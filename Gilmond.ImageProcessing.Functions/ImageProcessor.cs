using System;
using System.IO;
using ImageMagick;

namespace Gilmond.ImageProcessing.Functions
{
    public static class ImageProcessor
    {
        public static byte[] ProcessInstruction(ProcessingInstruction nextInstruction, Stream blobStream)
        {
            switch (nextInstruction.Operation)
            {
                case ProcessingOperation.Grayscale:
                    return ProcessGrayscale(blobStream);
                case ProcessingOperation.ResizeByWidth:
                    return ProcessResizeByWidth(blobStream, int.Parse(nextInstruction.Arguments[0]));
                case ProcessingOperation.ChangeFormat:
                    return ProcessChangeFormat(blobStream, nextInstruction.Arguments[0]);
                default:
                    throw new InvalidOperationException($"No processing operation defined for {nextInstruction.Operation}");
            }
        }

        private static byte[] ProcessChangeFormat(Stream blobStream, string format)
        {
            if (format == "png")
            {
                return ProcessImageAction(blobStream, image => image.Format = MagickFormat.Png);
            }
            throw new InvalidOperationException($"New format of {format} not supported");
        }

        private static byte[] ProcessImageAction(Stream blobStream, Action<MagickImage> action)
        {
            using (var image = new MagickImage(blobStream))
            {
                action(image);
                return image.ToByteArray();
            }
        }

        private static byte[] ProcessResizeByWidth(Stream blobStream, int width)
        {
            return ProcessImageAction(blobStream, image => image.Resize(width, 0));
        }

        private static byte[] ProcessGrayscale(Stream blobStream)
        {
            return ProcessImageAction(blobStream, image => image.Grayscale(PixelIntensityMethod.Average));
        }
    }
}