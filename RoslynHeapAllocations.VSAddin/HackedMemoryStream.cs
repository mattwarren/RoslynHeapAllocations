using System.Diagnostics;
using System.IO;

namespace RoslynHeapAllocations.VSAddin
{
    public class HackedMemoryStream : MemoryStream
    {
        private readonly byte[] buffer;

        /// <summary>
        /// Using a regular MemoryStream causes Exceptions when emitting IL code.
        /// So this hacked-together class overrides the relevant methods to make it work.
        /// 
        /// This was figured out by trial-and-error, I have no idea WHY these changes work???
        /// </summary>
        public HackedMemoryStream(byte[] buffer)
            : base(buffer)
        {
            this.buffer = buffer;
        }

        // We DELIBRATELY return 0 here, instead of base.Length
        public override long Length 
        { 
            get { return 0; } 
        }

        // We DELIBRATELY DON'T call base.SetLength() here
        public override void SetLength(long value)
        {
        }
    }
}
