using System;

namespace GIMcompress.ElementHelpers
{
    public class TrimmedCurveBuilder : IElementBuilder
    {
        private double _paramFirst;
        private double _paramLast;

        private TrimmedCurveBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            if (_paramLast <= _paramFirst)
                throw new InvalidGimElementException("Trimmed curve: last param must be greater than first param!");

            return new GimElement
            {
                ElementType = GimElementType.C_trimmed,
                ElementDoubles = [_paramFirst, _paramLast],
                Locals = [new GimLocal()]
            };
        }

        /// <inheritdoc/>
        public GimTrimmedCurve BuildAdapter() => new(BuildElement());

        /// <summary>
        /// Starts trimmed curve construction with param range.
        /// </summary>
        public static TrimmedCurveBuilder FromParams(double first, double last)
        {
            if (last <= first)
                throw new InvalidGimElementException("Trimmed curve: last param must be greater than first param!");
            return new TrimmedCurveBuilder { _paramFirst = first, _paramLast = last };
        }
    }
}
