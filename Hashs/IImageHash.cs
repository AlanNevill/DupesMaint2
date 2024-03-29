﻿namespace DupesMaint2
{
    using System;

    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.PixelFormats;
#pragma warning disable IDE1006 // Naming Styles

    /// <summary>
    /// Interface for perceptual image hashing algorithm.
    /// </summary>
    public interface IImageHash
    {
        /// <summary>Hash the image using the algorithm.</summary>
        /// <param name="image">image to calculate hash from.</param>
        /// <returns>hash value of the image.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <c>null</c>.</exception>
        ulong Hash(Image<Rgba32> image);
    }
}
