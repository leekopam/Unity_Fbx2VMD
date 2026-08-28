using System;

namespace Fbx2Vmd.FBXImporter
{
    public sealed class FBXImportException : Exception
    {
        public FBXImportException(string message)
            : base(message)
        {
        }

        public FBXImportException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
