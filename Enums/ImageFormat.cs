using System;

namespace CameraRecordingService.Enums
{
    /// <summary>
    /// Image format types supported for screenshots
    /// </summary>
    public enum ImageFormat
    {
        /// <summary>
        /// PNG format (lossless compression)
        /// </summary>
        PNG,

        /// <summary>
        /// JPG format (lossy compression, smaller size)
        /// </summary>
        JPG,

        /// <summary>
        /// BMP format (no compression, largest size)
        /// </summary>
        BMP
    }
}
