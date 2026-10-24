using System;

namespace GIMcompress
{
    public class GimException : Exception
    {
        public GimException(string message = "Invalid GimData, GimElement, or other Gim Entity", Exception e = null) : base(message, e)
        {
        }
    }

    public class InvalidGimElementException : GimException
    {
        public GimElement Element
        {
            get; private set;
        }

        public InvalidGimElementException(string message = "Invalid Element", GimElement element = null) : base(message)
        {
            Element = element;
        }
    }

    public class InvalidGimOperation : GimException
    {
        public InvalidGimOperation(string message = "Invalid GimData, GimElement, or other Gim Entity") : base(message)
        {
        }
    }
}
