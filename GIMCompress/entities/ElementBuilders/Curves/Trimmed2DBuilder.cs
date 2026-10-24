namespace GIMcompress.ElementHelpers
{
    public class Trimmed2DBuilder : IElementBuilder
    {
        private double _paramFirst;
        private double _paramLast;

        private Trimmed2DBuilder() { }

        /// <inheritdoc/>
        public GimElement BuildElement()
        {
            if (_paramLast <= _paramFirst)
                throw new InvalidGimElementException("Trimmed2D curve: last param must be greater than first param!");

            return new GimElement
            {
                ElementType = GimElementType.C_trimmed2D,
                ElementDoubles = [_paramFirst, _paramLast],
                Locals = [new GimLocal()]
            };
        }

        /// <inheritdoc/>
        public GimTrimmed2DCurve BuildAdapter() => new(BuildElement());

        /// <summary>
        /// Starts trimmed 2D curve construction with param range.
        /// </summary>
        public static Trimmed2DBuilder FromParams(double first, double last)
        {
            if (last <= first)
                throw new InvalidGimElementException("Trimmed2D curve: last param must be greater than first param!");
            return new Trimmed2DBuilder { _paramFirst = first, _paramLast = last };
        }
    }
}
