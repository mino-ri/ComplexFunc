using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace ComplexFunc
{
    public interface IComplexVisualizer
    {
        uint GetColor(Complex z);
    }

    public class CheckerComplexVisualizer : IComplexVisualizer
    {
        public uint GetColor(Complex z)
        {
            return ((int)Math.Floor(z.Real) + (int)Math.Floor(z.Imaginary)) % 2 == 0
                ? 0xFF231F1C
                : 0xFF337F51;
        }

        public override string ToString() => "市松模様";
    }

    public class HojoComplexVisualizer : IComplexVisualizer
    {
        public uint GetColor(Complex z)
        {
            return (int)Math.Floor(z.Imaginary) % 2 == 0
                ? (int)Math.Floor(z.Real) % 2 == 0 ? 0xFFC6733B : 0xFF667F51
                : (int)Math.Floor(z.Real) % 2 == 0 ? 0xFF231F1C : 0xFFF1E6DE;
        }

        public override string ToString() => "豊穣チェック";
    }

    public class RainbowComplexVisualizer : IComplexVisualizer
    {
        private readonly bool _magnitude;
        private readonly bool _checker;

        public RainbowComplexVisualizer(bool magnitude, bool checker)
        {
            _magnitude = magnitude;
            _checker = checker;
        }

        public uint GetColor(Complex z)
        {
            var phase = z.Phase / Math.PI * 3.0 + 3.0;
            var ratio = Math.Clamp(phase - Math.Floor(phase), 0.0, 1.0);
            double r = 0.0, g = 0.0, b = 0.0;

            if (phase < 0.0 || 6.0 <= phase)
            {
                g = 1.0;
                b = 1.0;
            }
            else if (phase < 1.0)
            {
                g = 1.0 - ratio;
                b = 1.0;
            }
            else if (phase < 2.0)
            {
                b = 1.0;
                r = ratio;
            }
            else if (phase < 3.0)
            {
                b = 1.0 - ratio;
                r = 1.0;
            }
            else if (phase < 4.0)
            {
                r = 1.0;
                g = ratio;
            }
            else if (phase < 5.0)
            {
                r = 1.0 - ratio;
                g = 1.0;
            }
            else
            {
                g = 1.0;
                b = ratio;
            }

            if (_magnitude)
            {
                var mag = z.Magnitude;
                if (!double.IsFinite(mag))
                {
                    return 0xFF000000;
                }
                if (mag > 1.0)
                {
                    mag = Math.Sqrt(1.0 / mag);
                    r = 1.0 - (1.0 - r) * mag;
                    g = 1.0 - (1.0 - g) * mag;
                    b = 1.0 - (1.0 - b) * mag;
                }
                else if (mag < 1.0)
                {
                    mag = Math.Sqrt(mag);
                    r *= mag;
                    g *= mag;
                    b *= mag;
                }
            }

            if (_checker)
            {
                if (((int)Math.Floor(z.Real) + (int)Math.Floor(z.Imaginary)) % 2 == 0)
                {
                    r *= 0.9375;
                    g *= 0.9375;
                    b *= 0.9375;
                }
            }

            return 0xFF000000 |
                   (uint)(255.0 * r) << 16 |
                   (uint)(255.0 * g) << 8 |
                   (uint)(255.0 * b);
        }

        public override string ToString() => "偏角虹色"
            + (_magnitude ? " + 絶対値明暗" : "")
            + (_checker ? " + 市松" : "");
    }
}
