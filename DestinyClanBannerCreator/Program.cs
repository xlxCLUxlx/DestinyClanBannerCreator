/*
 * MIT License
 *
 * Copyright(c) 2017 Eric Boulden (xlxCLUxlx on GitHub)
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:

 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.

 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

using Fclp;

using Newtonsoft.Json.Linq;

namespace DestinyClanBannerCreator
{
    /// <summary>
    /// A simple structure to for storing x,y offsets when drawing an image.
    /// </summary>
    public struct ImageSettings
    {
        #region Fields

        /// <summary>
        /// Gets or sets the image to be drawn.
        /// </summary>
        public Bitmap Bitmap;

        /// <summary>
        /// Gets or sets the x offset.
        /// </summary>
        public int XOffset;

        /// <summary>
        /// Gets or sets the y offset.
        /// </summary>
        public int YOffset;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageSettings"/> structure.
        /// </summary>
        /// <param name="Bitmap">The image that will be drawn.</param>
        /// <param name="XOffset">The x offset for the image.</param>
        /// <param name="YOffset">The y offset for the image.</param>
        public ImageSettings(Bitmap Bitmap, int XOffset, int YOffset)
        {
            this.Bitmap = Bitmap;
            this.XOffset = XOffset;
            this.YOffset = YOffset;
        }

        #endregion
    }

    /// <summary>
    /// A class used for storing the arugements that are passed to the application.  This class is used
    /// in conjunction with the Fluent Command Line Parser.
    /// https://github.com/PingmanTools/fluent-command-line-parser/tree/netstandard
    /// </summary>
    public class ApplicationArguments
    {
        #region Public Properties

        /// <summary>
        /// Gets or sets the databse to use for querying clann banner information.
        /// </summary>
        /// <remarks>This would be the clanbanner_sql_content database.</remarks>
        public String Database
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the hash that represents the background color of the decal (emblem).
        /// </summary>
        public String DecalBackgroundColorId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the hash that represents the forground color of the decal (emblem).
        /// </summary>
        public String DecalColorId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the hash that represents the decal image (emblem).
        /// </summary>
        public String DecalId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the hash that represents the color of the gonfalon (banner).
        /// </summary>
        public String GonfalonColorId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the hash that represents the detail color of the gonfalon detail (banner detail).
        /// </summary>
        public String GonfalonDetailColorId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the hash that represents the gonfalon detail image (banner detail).
        /// </summary>
        public String GonfalonDetailId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the hash that represents the gonfalon image (banner).
        /// </summary>
        public String GonfalonId
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets what to save teh final clan banner image as.
        /// </summary>
        public String SaveAs
        {
            get; set;
        }

        #endregion
    }

    /// <summary>
    /// The main program and execution logic.
    /// </summary>
    class Program
    {
        #region Fields

        /// <summary>
        /// The base address of the Bungie website for retreiving images.
        /// </summary>
        private const string BASE_ADDRESS = "https://www.bungie.net";

        /// <summary>
        /// The banner overlay image which is not part of the manifest.
        /// </summary>
        private const string FLAG_OVERLAY = "/img/bannercreator/flag_overlay.png";

        /// <summary>
        /// The banner stave image whih is not part of the manifest.
        /// </summary>
        private const string FLAG_STAFF = "/img/bannercreator/FlagStand00.png";

        #endregion

        #region Public Methods

        /// <summary>
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        /// <remarks>https://stackoverflow.com/a/24199315</remarks>
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Changes the color of an image by inspecting the images pixels.
        /// </summary>
        /// <param name="ColorToApply">The color that is being applied to the image.</param>
        /// <param name="ColorsToIgnore">Any colors that should be ignored and not overwritten.</param>
        /// <param name="Bitmap">The image that the color will be applied to.</param>
        /// <remakrs>
        /// I originally looked at using a ColorMap but it was properly handeling the alpha channel in all cases.
        /// This provided a much better result with minimal time.
        /// </remakrs>
        private static void ApplyColorChagesToImage(Color ColorToApply, List<Color> ColorsToIgnore, ref Bitmap Bitmap)
        {
            for (int y = 0; y < Bitmap.Height; y++)
            {
                for (int x = 0; x < Bitmap.Width; x++)
                {
                    Color c = Bitmap.GetPixel(x, y);
                    if (!ColorsToIgnore.Contains(c))
                        Bitmap.SetPixel(x, y, Color.FromArgb(c.A, ColorToApply));
                }
            }
        }

        /// <summary>
        /// This is the main method that is used for creating the cln banner image.
        /// </summary>
        /// <param name="Args">The arguements that were passed to the application.</param>
        private static void BuildClanBanner(ApplicationArguments Args)
        {
            // get the managed GDI+ png image codec.  This is what the managed GDI+ will use to encode the image
            // when the codec is passed to the Save function.
            ImageCodecInfo codec = GetEncoderInfo("image/png");
            if (codec == null)
                throw new NullReferenceException("Unable to get the ImageCodecInfo for image/png");

            // The Quality parameter specifies the level of compression for an image which is from 0 to 100.
            // The lower the number specified, the higher the compression and therefore the lower the quality of
            // the image. Zero would give you the lowest quality image and 100 the highest.
            Encoder encorderQuality = Encoder.Quality;
            EncoderParameter encoderParameterQuality = new EncoderParameter(encorderQuality, 90L);
            EncoderParameters codecParams = new EncoderParameters(1);
            codecParams.Param[0] = encoderParameterQuality;

            // go ahead and parse the hashes to unsigned integers.  There is no need to use uint.TryParse because
            // we tested the arguements earlier in our additional validation.
            uint decalId = uint.Parse(Args.DecalId);
            uint decalColorId = uint.Parse(Args.DecalColorId);
            uint decalBackgroundColorId = uint.Parse(Args.DecalBackgroundColorId);
            uint gonfalonId = uint.Parse(Args.GonfalonId);
            uint gonfalonColorId = uint.Parse(Args.GonfalonColorId);
            uint gonfalonDetailId = uint.Parse(Args.GonfalonDetailId);
            uint gonfalonDetailColorId = uint.Parse(Args.GonfalonDetailColorId);

            dynamic gonfalonJson = ExecuteQuery(Args.Database, string.Format("SELECT json FROM Gonfalons WHERE CASE WHEN (id < 0) THEN (id + 4294967296) ELSE id END = {0}", gonfalonId));
            byte[] gonfalonImageData = new System.Net.WebClient().DownloadData(string.Format("{0}{1}", BASE_ADDRESS, gonfalonJson.foregroundImagePath));
            Bitmap gonfalonImage;
            using (var ms = new MemoryStream(gonfalonImageData))
            {
                gonfalonImage = new Bitmap(ms);
            }

            byte[] flagStaffImageData = new System.Net.WebClient().DownloadData(string.Format("{0}{1}", BASE_ADDRESS, FLAG_STAFF));
            Bitmap flagStaffImage;
            using (var ms = new MemoryStream(flagStaffImageData))
            {
                flagStaffImage = new Bitmap(ms);
            }

            int masterWidth = flagStaffImage.Width;
            int masterHeight = flagStaffImage.Height;
            flagStaffImage = ScaleImage(flagStaffImage, 422, 616);

            byte[] flagOverlayImageData = new System.Net.WebClient().DownloadData(string.Format("{0}{1}", BASE_ADDRESS, FLAG_OVERLAY));
            Bitmap flagOverlayImage;
            using (var ms = new MemoryStream(flagOverlayImageData))
            {
                flagOverlayImage = new Bitmap(ms);
            }

            // We need to shift the overlay image so that it lines up properly with the gonfalon for clipping.
            Bitmap b = new Bitmap(flagOverlayImage.Width, flagOverlayImage.Height);
            b.SetResolution(flagOverlayImage.HorizontalResolution, flagOverlayImage.VerticalResolution);
            Graphics g = Graphics.FromImage(b);
            Rectangle rect = new Rectangle(35, 0, gonfalonImage.Width, gonfalonImage.Height);
            g.DrawImage(gonfalonImage, rect, 0, 0, gonfalonImage.Width, gonfalonImage.Height, GraphicsUnit.Pixel);

            ClipBitmapBasedOnGonfalon(b, ref flagOverlayImage, false);

            Bitmap decal = BuildDecalImage(Args.Database, gonfalonImage, decalId, decalColorId, decalBackgroundColorId);

            Bitmap gonfalon = BuildGonfalonImage(Args.Database, gonfalonImage, gonfalonColorId, gonfalonDetailId, gonfalonDetailColorId);

            Stack<ImageSettings> images = new Stack<ImageSettings>();
            images.Push(new ImageSettings(flagStaffImage, 38, 0));
            images.Push(new ImageSettings(flagOverlayImage, 12, 42));
            images.Push(new ImageSettings(decal, 48, 42));
            images.Push(new ImageSettings(gonfalon, 48, 42));

            Bitmap clanBanner = Merge(images, masterWidth, masterHeight, flagStaffImage.HorizontalResolution, flagStaffImage.VerticalResolution);
            clanBanner.Save(Args.SaveAs, codec, codecParams);
        }

        /// <summary>
        /// Processes the the components which make up the decal for the clan banner.
        /// </summary>
        /// <param name="Database">The path to the database that contains the clanbanner_sql_content.</param>
        /// <param name="GonfalonImage">The image of the gonfalon.</param>
        /// <param name="DecalId">The hash that represents the decal foreground and background images to use.  This hash is referenced against the Decals table in the clanbanner_sql_content database.</param>
        /// <param name="DecalColorId">The hash that represents the color to use on the decal foreground image.  This hash is referenced against the DecalPrimaryColors table in the clanbanner_sql_content database.</param>
        /// <param name="DecalBackgroundColorId">The hash that represents the color to use on the decal background image.  This hash is referenced against the DecalSecondaryColors table in the clanbanner_sql_content database.</param>
        /// <returns>The constructed decal image to be used on the clan banner.</returns>
        private static Bitmap BuildDecalImage(string Database, Bitmap GonfalonImage, uint DecalId, uint DecalColorId, uint DecalBackgroundColorId)
        {
            Bitmap decal = null;
            List<Color> transparentIgnore = new List<Color>() { Color.FromArgb(0, 0, 0, 0) };

            dynamic decalJson = ExecuteQuery(Database, string.Format("SELECT json FROM Decals WHERE CASE WHEN (id < 0) THEN (id + 4294967296) ELSE id END = {0}", DecalId));
            byte[] decalForegroundImageData = new System.Net.WebClient().DownloadData(string.Format("{0}{1}", BASE_ADDRESS, decalJson.foregroundImagePath));
            Bitmap decalForegroundImage;
            using (var ms = new MemoryStream(decalForegroundImageData))
            {
                decalForegroundImage = new Bitmap(ms);
            }

            byte[] decalBackgroundImageData = new System.Net.WebClient().DownloadData(string.Format("{0}{1}", BASE_ADDRESS, decalJson.backgroundImagePath));
            Bitmap decalBackgroundImage;
            using (var ms = new MemoryStream(decalBackgroundImageData))
            {
                decalBackgroundImage = new Bitmap(ms);
            }

            dynamic decalColorJson = ExecuteQuery(Database, string.Format("SELECT json FROM DecalPrimaryColors WHERE CASE WHEN (id < 0) THEN (id + 4294967296) ELSE id END = {0}", DecalColorId));
            string dRed = decalColorJson.red;
            string dGreen = decalColorJson.green;
            string dBlue = decalColorJson.blue;
            Color decalColor = Color.FromArgb(int.Parse(dRed), int.Parse(dGreen), int.Parse(dBlue));
            ApplyColorChagesToImage(decalColor, transparentIgnore, ref decalForegroundImage);

            dynamic decalBackgroundColorJson = ExecuteQuery(Database, string.Format("SELECT json FROM DecalSecondaryColors WHERE CASE WHEN (id < 0) THEN (id + 4294967296) ELSE id END = {0}", DecalBackgroundColorId));
            string dbRed = decalBackgroundColorJson.red;
            string dbGreen = decalBackgroundColorJson.green;
            string dbBlue = decalBackgroundColorJson.blue;
            Color decalBackgroundColor = Color.FromArgb(int.Parse(dbRed), int.Parse(dbGreen), int.Parse(dbBlue));
            ApplyColorChagesToImage(decalBackgroundColor, transparentIgnore, ref decalBackgroundImage);

            Stack<ImageSettings> images = new Stack<ImageSettings>();
            images.Push(new ImageSettings(decalForegroundImage, 0, 0));
            images.Push(new ImageSettings(decalBackgroundImage, 0, 0));

            decal = Merge(images, decalBackgroundImage.Width, decalBackgroundImage.Height, decalBackgroundImage.HorizontalResolution, decalBackgroundImage.VerticalResolution);
            ClipBitmapBasedOnGonfalon(GonfalonImage, ref decal, true);

            return decal;
        }

        /// <summary>
        ///  Processes the the components which make up the gonfalon for the clan banner.
        /// </summary>
        /// <param name="Database">The path to the database that contains the clanbanner_sql_content.</param>
        /// <param name="GonfalonImage">The image of the gonfalon.</param>
        /// <param name="GonfalonColorId">The hash that represents the color to use on the gonfalon image.  This hash is referenced against the GonfalonColors table in the clanbanner_sql_content database.</param>
        /// <param name="GonfalonDetailId">The hash that represents the gonfalon detail image to use.  This hash is referenced against the GonfalonDetails table in the clanbanner_sql_content database.</param>
        /// <param name="GonfalonDetailColorId">The hash that represents the color to use on the gonfalon detail image.  This hash is referenced against the GonfalonDetailColors table in the clanbanner_sql_content database.</param>
        /// <returns>>The constructed decal image to be used on the clan banner.</returns>
        private static Bitmap BuildGonfalonImage(string Database, Bitmap GonfalonImage, uint GonfalonColorId, uint GonfalonDetailId, uint GonfalonDetailColorId)
        {
            Bitmap gonfalon = null;
            List<Color> transparentIgnore = new List<Color>() { Color.FromArgb(0, 0, 0, 0) };

            dynamic gonfalonDetailJson = ExecuteQuery(Database, string.Format("SELECT json FROM GonfalonDetails WHERE CASE WHEN (id < 0) THEN (id + 4294967296) ELSE id END = {0}", GonfalonDetailId));
            byte[] gonfalonDetailImageData = new System.Net.WebClient().DownloadData(string.Format("{0}{1}", BASE_ADDRESS, gonfalonDetailJson.foregroundImagePath));
            Bitmap gonfalonDetailImage;
            using (var ms = new MemoryStream(gonfalonDetailImageData))
            {
                gonfalonDetailImage = new Bitmap(ms);
            }

            dynamic gonfalonDetailColorJson = ExecuteQuery(Database, string.Format("SELECT json FROM GonfalonDetailColors WHERE CASE WHEN (id < 0) THEN (id + 4294967296) ELSE id END = {0}", GonfalonDetailColorId));
            string dRed = gonfalonDetailColorJson.red;
            string dGreen = gonfalonDetailColorJson.green;
            string dBlue = gonfalonDetailColorJson.blue;
            Color gonfalonDetailColor = Color.FromArgb(int.Parse(dRed), int.Parse(dGreen), int.Parse(dBlue));
            ApplyColorChagesToImage(gonfalonDetailColor, transparentIgnore, ref gonfalonDetailImage);
            ClipBitmapBasedOnGonfalon(GonfalonImage, ref gonfalonDetailImage, true);

            dynamic gonfalonColorJson = ExecuteQuery(Database, string.Format("SELECT json FROM GonfalonColors WHERE CASE WHEN (id < 0) THEN (id + 4294967296) ELSE id END = {0}", GonfalonColorId));
            string gRed = gonfalonColorJson.red;
            string gGreen = gonfalonColorJson.green;
            string gBlue = gonfalonColorJson.blue;
            Color gonfalonColor = Color.FromArgb(int.Parse(gRed), int.Parse(gGreen), int.Parse(gBlue));
            ApplyColorChagesToImage(gonfalonColor, transparentIgnore, ref GonfalonImage);

            Stack<ImageSettings> images = new Stack<ImageSettings>();
            images.Push(new ImageSettings(gonfalonDetailImage, 0, 0));
            images.Push(new ImageSettings(GonfalonImage, 0, 0));

            gonfalon = Merge(images, GonfalonImage.Width, GonfalonImage.Height, GonfalonImage.HorizontalResolution, GonfalonImage.VerticalResolution);

            return gonfalon;
        }

        /// <summary>
        /// A poor mans clipping function that I developed to clip images based on the transparency of another image.
        /// </summary>
        /// <param name="BitmapGonfalon">The gonfalon image to use as our clipping mask.  Since everything sits on this image it is used as the mask.</param>
        /// <param name="BitmapToClip">The image to clip based on the gonfalon mask (image).</param>
        /// <param name="ModifyExistingColors">A flag as to whether or not to overwrite exiting colors when clipping (i.e. making pixels transparent).</param>
        /// <remarks>
        /// Once again I had originally planned on using Graphics.SetClip method; however, it was not providing the results I wanted.
        /// I basically modified my <see cref="ApplyColorChagesToImage"/> strategy which let me acheive the results I was looking for
        /// without taking a performance hit.
        /// </remarks>
        private static void ClipBitmapBasedOnGonfalon(Bitmap BitmapGonfalon, ref Bitmap BitmapToClip, bool ModifyExistingColors = true)
        {
            Color transparent = Color.FromArgb(0, 0, 0, 0);
            int x = 0, y = 0;

            for (y = 0; y < BitmapToClip.Height; y++)
            {
                for (x = 0; x < BitmapToClip.Width; x++)
                {
                    if (x >= BitmapGonfalon.Width || y >= BitmapGonfalon.Height)
                    {
                        BitmapToClip.SetPixel(x, y, transparent);
                    }
                    else
                    {
                        Color c = BitmapGonfalon.GetPixel(x, y);

                        if (c.Equals(transparent))
                        {
                            BitmapToClip.SetPixel(x, y, transparent);
                        }
                        else
                        {
                            if (ModifyExistingColors)
                            {
                                if (c.A != 255)
                                {
                                    Color existingColor = BitmapToClip.GetPixel(x, y);

                                    if (!existingColor.Equals(transparent))
                                    {
                                        Color newColor = Color.FromArgb(c.A, existingColor);
                                        BitmapToClip.SetPixel(x, y, newColor);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Quick and dirty method to query the clanbanner_sql_content database and return the json from the table.
        /// </summary>
        /// <param name="Database">The database to connect to (i.e. clanbanner_sql_content).</param>
        /// <param name="Query">The query to execute against the database.</param>
        /// <remarks>This is set to always return the json field from the query.</remarks>
        /// <returns>The json from the table being queried.</returns>
        private static dynamic ExecuteQuery(string Database, string Query)
        {
            // validate we can connect to the database (i.e. clanbanner_sql_Content).
            using (var sqConn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", Database)))
            {
                sqConn.Open();

                using (var sqCmd = new SQLiteCommand(Query, sqConn))
                {
                    using (var sqReader = sqCmd.ExecuteReader())
                    {
                        if (sqReader.Read())
                        {
                            byte[] jsonData = (byte[])sqReader["json"];
                            string json = System.Text.Encoding.ASCII.GetString(jsonData);
                            dynamic dyn = JObject.Parse(json);
                            return dyn;
                        }
                    }
                }

                sqConn.Close();
            }

            return null;
        }

        /// <summary>
        /// Gets the encoder based on the mimeType (Multipurpose Internet Mail Extensions).
        /// </summary>
        /// <param name="mimeType">The mime type to get (i.e. image/png)</param>
        /// <returns>The codec information for the mime type specified.</returns>
        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }

        /// <summary>
        /// The main program execution entry point.
        /// </summary>
        /// <param name="args">The arguements passed into the application.</param>
        static void Main(string[] args)
        {
            try
            {
                var p = new FluentCommandLineParser<ApplicationArguments>();
                p.Parser.IsCaseSensitive = false;
                p.Parser.SetupHelp("?", "help")
                    .Callback(text => Console.WriteLine(text));

                p.Setup(arg => arg.Database)
                   .As("database")
                   .Required();

                p.Setup(arg => arg.SaveAs)
                   .As("saveAs")
                   .Required();

                p.Setup(arg => arg.DecalId)
                    .As("decalId")
                    .Required();

                p.Setup(arg => arg.DecalColorId)
                    .As("decalColorId")
                    .Required();

                p.Setup(arg => arg.DecalBackgroundColorId)
                    .As("decalBackgroundColorId")
                    .Required();

                p.Setup(arg => arg.GonfalonId)
                    .As("gonfalonId")
                    .Required();

                p.Setup(arg => arg.GonfalonColorId)
                    .As("gonfalonColorId")
                    .Required();

                p.Setup(arg => arg.GonfalonDetailId)
                    .As("gonfalonDetailId")
                    .Required();

                p.Setup(arg => arg.GonfalonDetailColorId)
                    .As("gonfalonDetailColorId")
                    .Required();

                var result = p.Parse(args);
                if (result.HasErrors)
                {
                    Console.WriteLine("The following arguements are required to run the application:");
                    p.Parser.HelpOption.ShowHelp(p.Parser.Options);
                    return;
                }
                else
                {
                    if (!PassesAdditionalValidation(p.Object))
                        return;
                }

                BuildClanBanner(p.Object);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An Exception has occured.  Please refer to the StackTrace below.");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// Merges a stack of images into one image.
        /// </summary>
        /// <param name="Images">The stack of images to merge together.</param>
        /// <param name="Width">The width of the new image.</param>
        /// <param name="Height">The height of the new image.</param>
        /// <param name="HorizontalResolution">The horizontal resolution, in pixels per inch to set for the new image.</param>
        /// <param name="VerticalResolution">The vertical resolution, in pixels per inch to set for the new image.</param>
        /// <returns></returns>
        /// <remarks>Since a Stack in a LIFO (Last in First out) data structure you will push the images to the stack in the reverse order of how they will be drawn.</remarks>
        private static Bitmap Merge(Stack<ImageSettings> Images, int Width, int Height, float HorizontalResolution, float VerticalResolution)
        {
            var target = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            target.SetResolution(HorizontalResolution, VerticalResolution);
            var graphics = Graphics.FromImage(target);
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;

            for (int i = Images.Count; i > 0; i--)
            {
                ImageSettings imageSettings = Images.Pop();
                Rectangle rect = new Rectangle(imageSettings.XOffset, imageSettings.YOffset, imageSettings.Bitmap.Width, imageSettings.Bitmap.Height);
                graphics.DrawImage(imageSettings.Bitmap, rect, 0, 0, imageSettings.Bitmap.Width, imageSettings.Bitmap.Height, GraphicsUnit.Pixel);
            }

            return target;
        }

        /// <summary>
        /// Performs additional checks on the arguements that were passed to the application.
        /// </summary>
        /// <param name="Args">The arguements that were passed to the application.</param>
        /// <returns><c>true</c> if there were no validation errors and <c>false</c> if there were validation errors.</returns>
        private static bool PassesAdditionalValidation(ApplicationArguments Args)
        {
            try
            {
                // validate we can connect to the database (i.e. clanbanner_sql_Content).
                using (var sqConn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", Args.Database)))
                {
                    sqConn.Open();
                    sqConn.Close();
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to create a connection to the SQLite database {0}.", Args.Database);
                return false;
            }

            try
            {
                uint.Parse(Args.DecalId);
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to cast {0} to an unsigned integer.", Args.DecalId);
                return false;
            }

            try
            {
                uint.Parse(Args.DecalColorId);
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to cast {0} to an unsigned integer.", Args.DecalColorId);
                return false;
            }

            try
            {
                uint.Parse(Args.DecalBackgroundColorId);
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to cast {0} to an unsigned integer.", Args.DecalBackgroundColorId);
                return false;
            }

            try
            {
                uint.Parse(Args.GonfalonId);
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to cast {0} to an unsigned integer.", Args.GonfalonId);
                return false;
            }

            try
            {
                uint.Parse(Args.GonfalonColorId);
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to cast {0} to an unsigned integer.", Args.GonfalonColorId);
                return false;
            }

            try
            {
                uint.Parse(Args.GonfalonDetailId);
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to cast {0} to an unsigned integer.", Args.GonfalonDetailId);
                return false;
            }

            try
            {
                uint.Parse(Args.GonfalonDetailColorId);
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to cast {0} to an unsigned integer.", Args.GonfalonDetailColorId);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Scales an image proportionately based on the width and height specified.
        /// </summary>
        /// <param name="Bitmap">The image to scale.</param>
        /// <param name="Width">The width to scale to.</param>
        /// <param name="Height">The height to scale to.</param>
        /// <returns>The scaled image.</returns>
        /// <remarks>https://stackoverflow.com/a/6501997</remarks>
        private static Bitmap ScaleImage(Bitmap Bitmap, int Width, int Height)
        {
            var ratioX = (double)Width / Bitmap.Width;
            var ratioY = (double)Height / Bitmap.Height;
            var ratio = Math.Max(ratioX, ratioY);

            var newWidth = (int)(Bitmap.Width * ratio);
            var newHeight = (int)(Bitmap.Height * ratio);

            var newImage = new Bitmap(newWidth, newHeight);

            using (var graphics = Graphics.FromImage(newImage))
                graphics.DrawImage(Bitmap, 0, 0, newWidth, newHeight);

            return newImage;
        }

        #endregion
    }
}