using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ComplexFunc
{
    public class ViewModel : INotifyPropertyChanged
    {
        public int ImageSize { get; } = 512;

        private readonly WriteableBitmap _bitmap;

        private Func<Complex , Complex> _f = (Complex z) => Complex.Sin(z);

        public ImageSource Graph => _bitmap;

        private readonly double[] _scales =
        {
            0.015625,
            0.0234375,
            0.03125,
            0.046875,
            0.0625,
            0.09375,
            0.125,
            0.1875,
            0.25,
            0.375,
            0.5,
            0.75,
            1.0,
            1.5,
            2.0,
            3.0,
            4.0,
            6.0,
            8.0,
            12.0,
            16.0,
            24.0,
            32.0,
        };

        public double Scale => _scales[_scaleIndex];

        public int MaxScaleIndex => _scales.Length - 1;

        private int _scaleIndex = 12;
        public int ScaleIndex
        {
            get => _scaleIndex;
            set
            {
                if (SetValue(ref _scaleIndex, value))
                {
                    OnPropertyChanged(nameof(Scale));
                    RewriteBitmap();
                }
            }
        }

        private double _centerX = 0.0;
        public double CenterX { get => _centerX; set => SetValueAndRedraw(ref _centerX, value); }

        private double _centerY = 0.0;
        public double CenterY { get => _centerY; set => SetValueAndRedraw(ref _centerY, value); }

        private bool _showAxis = false;
        public bool ShowAxis { get => _showAxis; set => SetValueAndRedraw(ref _showAxis, value); }

        private bool _solidMode = true;
        public bool SolidMode { get => _solidMode; set => SetValueAndRedraw(ref _solidMode, value); }

        private double _pitch = Math.PI / 4.0;
        public double Pitch { get => _pitch; set => SetValueAdnRedrawWhenSolidMode(ref _pitch, value); }

        private double _yaw = Math.PI / 18.0;
        public double Yaw { get => _yaw; set => SetValueAdnRedrawWhenSolidMode(ref _yaw, value); }

        private bool _showImaginary3D = true;
        public bool ShowImaginary3D { get => _showImaginary3D; set => SetValueAdnRedrawWhenSolidMode(ref _showImaginary3D, value); }

        public void SetCenter(double x, double y)
        {
            if (SetValue(ref _centerX, x, nameof(CenterX)) | SetValue(ref _centerY, y, nameof(CenterY)))
                RewriteBitmap();
        }

        public void SetRotation(double yaw, double pitch)
        {
            if (SetValue(ref _yaw, yaw, nameof(Yaw)) | SetValue(ref _pitch, pitch, nameof(Pitch)))
            {
                if (SolidMode) RewriteBitmap();
            }
        }

        public IComplexVisualizer[] Visualizers { get; }

        private IComplexVisualizer _currentVisualizer;
        public IComplexVisualizer CurrentVisualizer { get => _currentVisualizer; set => SetValueAndRedraw(ref _currentVisualizer, value); }

        private string _expression = "sin x";
        public string Expression
        {
            get => _expression;
            set
            {
                if (SetValue(ref _expression, value))
                {
                    _f = ExpressionParser.Parse(_expression);
                    RewriteBitmap();
                }
            }
        }

        public ICommand ResetCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ViewModel()
        {
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (ushort.TryParse(arg, out var num) && num > 512)
                {
                    ImageSize = num;
                    break;
                }
            }

            _bitmap = new WriteableBitmap(ImageSize, ImageSize, 96.0, 96.0, PixelFormats.Pbgra32, null);
            Visualizers = new IComplexVisualizer[]
            {
                new RainbowComplexVisualizer(false, false),
                new RainbowComplexVisualizer(true, false),
                new RainbowComplexVisualizer(false, true),
                new RainbowComplexVisualizer(true, true),
                new CheckerComplexVisualizer(),
                new HojoComplexVisualizer(),
            };

            _currentVisualizer = Visualizers[1];
            ResetCommand = new ResetCenterCommand(this);
        }

        public void Initialize()
        {
            RewriteBitmap();
        }

        public void ResetCenter()
        {
            SolidMode = false;
            SetCenter(0.0, 0.0);
            SetRotation(0.0, 0.0);
        }

        private void RewriteBitmap() => WriteBitmap(_f, Scale);

        private void WriteBitmap(Func<Complex, Complex> f, double size)
        {
            try
            {
                _bitmap.Lock();
                if (SolidMode)
                    WriteBitmap3D(f, size, _bitmap.BackBuffer);
                else
                    WriteBitmapCore(f, size, _bitmap.BackBuffer);
                _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bitmap.PixelWidth, _bitmap.PixelHeight));
            }
            finally
            {
                _bitmap.Unlock();
            }
        }

        private void WriteBitmapCore(Func<Complex, Complex> f, double size, IntPtr buffer)
        {
            var imageSize = ImageSize;
            var minX = _centerX - size;
            var maxY = _centerY + size;
            var interval = size / (imageSize / 2);
            var stride = _bitmap.BackBufferStride / 4;
            var axisX = _showAxis ? (int)(-minX / interval) : -1;
            var axisY = _showAxis ? (int)(maxY / interval) : -1;

            Span<uint> p;
            unsafe { p = new Span<uint>(buffer.ToPointer(), _bitmap.PixelHeight * stride); }

            for (var y = 0; y < imageSize; y++)
            {
                if (y == axisY)
                {
                    for (var x = 0; x < imageSize; x++)
                        p[y * stride + x] = 0xFF000000;
                }
                else
                {
                    var im = maxY - interval * y;
                    for (var x = 0; x < imageSize; x++)
                    {
                        p[y * stride + x] = x == axisX
                            ? 0xFF000000
                            : _currentVisualizer.GetColor(f(new Complex(minX + interval * x, im)));
                    }
                }
            }
        }

        private void WriteBitmap3D(Func<Complex, Complex> f, double size, IntPtr buffer)
        {
            var imageSize = ImageSize;
            var center = imageSize / 2;
            var centerD = (double)center;
            var minX = _centerX - size;
            var maxY = _centerY + size;
            var interval = size / center;
            var axisThickness = interval / 2.0;
            var stride = _bitmap.BackBufferStride / 4;

            var cosPitch = Math.Cos(Pitch);
            var sinPitch = Math.Sin(Pitch);
            var cosYaw = Math.Cos(Yaw);
            var sinYaw = Math.Sin(Yaw);
            var cosYawDCosPitch = cosYaw / cosPitch;
            var sinYawDCosPitch = sinYaw / cosPitch;
            var cosYawCosPitch = cosYaw * cosPitch;
            var sinYawCosPitch = sinYaw * cosPitch;
            var reversed = cosPitch < 0.0;

            Span<uint> p;
            unsafe { p = new Span<uint>(buffer.ToPointer(), _bitmap.PixelHeight * stride); }

            p.Clear();
            for (var y = 0; y < imageSize; y++)
            { 
                for (var x = 0; x < imageSize; x++)
                {
                    var sourceX = (x - center) * cosYaw + (y - center) * sinYawDCosPitch + centerD;
                    var sourceY = -(x - center) * sinYaw + (y - center) * cosYawDCosPitch + centerD;
                    if (0 <= sourceX && sourceX < imageSize && 0 <= sourceY && sourceY < imageSize)
                    {
                        var re = minX + interval * sourceX;
                        var im = maxY - interval * sourceY;

                        if (_showAxis && (-axisThickness <= re && re <= axisThickness || -axisThickness <= im && im <= axisThickness))
                            p[y * stride + x] = 0xFF000000;
                        else
                            p[y * stride + x] = _currentVisualizer.GetColor(f(new Complex(re, im)));
                    }
                }
            }

            var (minSourceY, maxSourceY) = _showImaginary3D 
                ? (0, imageSize)
                : ((int)(maxY / interval), (int)(maxY / interval) + 1);

            for (var sourceY = minSourceY; sourceY < maxSourceY; sourceY++)
            {
                for (var sourceX = 0; sourceX < imageSize; sourceX++)
                {
                    var value = f(new Complex(minX + interval * sourceX, maxY - interval * sourceY));
                    var phase = Math.Abs(value.Phase);
                    if (phase > 0.03 && 3.111 > phase) continue;
                    var x = (int)Math.Floor((sourceX - center) * cosYaw - (sourceY - center) * sinYaw) + center;
                    if (0 > x || x >= imageSize) continue;
                    var y = (int)Math.Floor((sourceX - center) * sinYawCosPitch + (sourceY - center) * cosYawCosPitch) + center;
                    var z = y - (int)Math.Floor(value.Real * sinPitch / interval);
                    if (0 > z || z >= imageSize) continue;

                    var front = reversed ^ (0.0 <= value.Real);
                    if (!front)
                    {
                        var sx = (x - center) * cosYaw + (z - center) * sinYawDCosPitch + centerD;
                        var sy = -(x - center) * sinYaw + (z - center) * cosYawDCosPitch + centerD;
                        if (!(0 <= sx && sx < imageSize && 0 <= sy && sy < imageSize))
                            front = true;
                    }

                    if (front)
                    {
                        if (0 < z) p[(z - 1) * stride + x] = 0xFF000000;
                        p[z * stride + x] = 0xFF000000;
                        if (z < imageSize - 1) p[(z + 1) * stride + x] = 0xFF000000;
                    }
                }
            }

            if (_showAxis)
            {
                var axisX = -minX / interval;
                var axisY = maxY / interval;

                if (0 <= axisX && axisX < imageSize && 0 <= axisY && axisY < imageSize)
                {
                    var dx = (int)Math.Floor((axisX - centerD) * cosYaw - (axisY - centerD) * sinYaw) + center;
                    var dy = (int)Math.Floor((axisX - centerD) * sinYawCosPitch + (axisY - centerD) * cosYawCosPitch) + center;

                    if (0 <= dx && dx < imageSize)
                    {
                        for (var y = 0; y < imageSize; y++)
                        {
                            var front = y < dy ^ reversed;
                            if (!front)
                            {
                                var sx = (dx - center) * cosYaw + (y - center) * sinYawDCosPitch + centerD;
                                var sy = -(dx - center) * sinYaw + (y - center) * cosYawDCosPitch + centerD;
                                if (!(0 <= sx && sx < imageSize && 0 <= sy && sy < imageSize))
                                    front = true;
                            }

                            if (front) p[y * stride + dx] = 0xFF000000;
                        }
                    }
                }
            }
        }

        private bool SetValue<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (Equals(storage, value)) return false;

            storage = value;
            OnPropertyChanged(propertyName);

            return true;
        }

        private void SetValueAndRedraw<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (SetValue(ref storage, value, propertyName))
                RewriteBitmap();
        }

        private void SetValueAdnRedrawWhenSolidMode<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (SetValue(ref storage, value, propertyName) && SolidMode)
                RewriteBitmap();
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private class ResetCenterCommand : ICommand
        {
            private readonly ViewModel _parent;
            public ResetCenterCommand(ViewModel parent) => _parent = parent;

            public event EventHandler CanExecuteChanged { add { } remove { } }

            public bool CanExecute(object parameter) => true;

            public void Execute(object parameter) => _parent.ResetCenter();
        }
    }
}
