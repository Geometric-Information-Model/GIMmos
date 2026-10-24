namespace GIMcompress.ElementHelpers
{
    public interface IElementBuilder
    {
        /// <summary>
        /// Builds the final GimElement
        /// </summary>
        /// <returns>The Built GimElement.</returns>
        public GimElement BuildElement();
    }
}
