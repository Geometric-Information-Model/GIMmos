namespace GIMcompress.ElementHelpers
{
    public abstract class AbstractElementAdapter
    {
        protected readonly GimElement element;

        /// <summary>
        /// The specific GimElementType that this adapter accepts.
        /// Override in concrete adapters.
        /// </summary>
        protected abstract GimElementType ExpectedType { get; }

        /// <summary>
        /// The orientation of the element
        /// </summary>
        public bool Orientation
        {
            get { return element.Orientation; }
            set { element.Orientation = value; }
        }

        /// <summary>
        /// The number of instances (locals) of this element
        /// </summary>
        public int InstanceCount
        {
            get
            {
                return element.Locals != null ? element.Locals.Count : 0;
            }
        }

        public AbstractElementAdapter(GimElement element)
        {
            this.element = element;
            if (element.ElementType != ExpectedType)
                throw new InvalidGimElementException(
                    $"Expected {ExpectedType} but got {element.ElementType}", element);
        }

        /// <summary>
        /// The GimElement being wrapped
        /// </summary>
        public GimElement Element
        {
            get { return element; }
        }

        /// <summary>
        /// Flips the element's orientation
        /// </summary>
        /// <returns>The new orientation</returns>
        public bool FlipOrientation()
        {
            Orientation = !Orientation;
            return Orientation;
        }

        /// <summary>
        /// Asserts that an element has locals. Throws an <see cref="InvalidGimOperation"></see> if the element has no locals.
        /// Should be used before operations that assume an element has locals.
        /// </summary>
        /// <exception cref="InvalidGimOperation"></exception>
        public void AssertElementHasLocals()
        {
            if (InstanceCount == 0)
                throw new InvalidGimOperation("Element has no locals!");
        }
    }
}
