using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;

namespace ComplexFunc
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private double _x, _y;
        private Point _dragStart;
        private DraggingMode _draggingMode = DraggingMode.None;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            if (DataContext is ViewModel vm)
            {
                vm.Initialize();

                var dpi = VisualTreeHelper.GetDpi(MainImage);
                MainImage.Width = vm.ImageSize / dpi.DpiScaleX;
                MainImage.Height = vm.ImageSize / dpi.DpiScaleY;
            }
        }

        private void MainImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModel vm)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    if (e.ClickCount == 2)
                    {
                        vm.SolidMode = false;
                        vm.SetRotation(0.0, 0.0);
                    }
                    else if (e.ClickCount < 2)
                    {
                        _draggingMode = DraggingMode.Left;
                        _x = vm.CenterX;
                        _y = vm.CenterY;
                        _dragStart = e.GetPosition(MainImage);
                        MainImage.CaptureMouse();
                    }
                }
                else if (e.RightButton == MouseButtonState.Pressed)
                {
                    _draggingMode = DraggingMode.Right;
                    _x = vm.Yaw;
                    _y = vm.Pitch;
                    _dragStart = e.GetPosition(MainImage);
                    MainImage.CaptureMouse();
                }
                else
                {
                    _draggingMode = DraggingMode.None;
                    MainImage.ReleaseMouseCapture();
                }
            }
        }

        private void MainImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingMode == DraggingMode.Left)
            {
                if (DataContext is ViewModel vm)
                {
                    var dpi = VisualTreeHelper.GetDpi(MainImage);
                    var scale = vm.Scale / (vm.ImageSize / 2);
                    var delta = e.GetPosition(MainImage) - _dragStart;

                    if (vm.SolidMode)
                    {
                        var dx = delta.X * scale * dpi.DpiScaleX;
                        var dy = delta.Y * scale * dpi.DpiScaleY;
                        var cosYaw = Math.Cos(vm.Yaw);
                        var sinYaw = Math.Sin(vm.Yaw);
                        var sign = vm.Pitch >= Math.PI / 2.0 ? -1.0 : 1.0;

                        vm.SetCenter(_x - (dx  * cosYaw + sign * dy * sinYaw), _y + (sign * dy * cosYaw - dx * sinYaw));
                    }
                    else
                    {
                        vm.SetCenter(_x - delta.X * scale * dpi.DpiScaleX, _y + delta.Y * scale * dpi.DpiScaleY);
                    }
                }
            }
            else if (_draggingMode == DraggingMode.Right)
            {
                if (DataContext is ViewModel vm)
                {
                    var dpi = VisualTreeHelper.GetDpi(MainImage);
                    var scale = Math.PI / vm.ImageSize;
                    var delta = e.GetPosition(MainImage) - _dragStart;

                    vm.SolidMode = true;
                    vm.SetRotation((_x - delta.X * scale * dpi.DpiScaleX) % (Math.PI * 2.0),
                                   Math.Clamp(_y - delta.Y * scale * dpi.DpiScaleY, 0, Math.PI));
                }
            }
        }

        private void MainImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DataContext is ViewModel vm)
            {
                vm.ScaleIndex = Math.Clamp(e.Delta > 0 ? vm.ScaleIndex - 1 : vm.ScaleIndex + 1, 0, vm.MaxScaleIndex);
            }
        }

        private enum DraggingMode
        {
            None, Left, Right,
        }
    }
}
