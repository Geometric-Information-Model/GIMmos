using System;
using System.IO;

namespace GIMtransfer.KlicImkl
{
    /// <summary>
    /// Counts bytes read from the underlying stream and forwards each non-empty block to a
    /// callback, so a forward-only payload reader can report progress without buffering the
    /// document. Shared by the IMKL and the generic GML/KML readers.
    /// </summary>
    internal sealed class ProgressReportingStream : Stream
    {
        private readonly Stream inner;
        private readonly Action<int> onRead;

        public ProgressReportingStream(Stream inner, Action<int> onRead)
        {
            this.inner = inner;
            this.onRead = onRead;
        }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = inner.Read(buffer, offset, count);
            if (read > 0) onRead(read);
            return read;
        }

        public override void Flush() => inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
