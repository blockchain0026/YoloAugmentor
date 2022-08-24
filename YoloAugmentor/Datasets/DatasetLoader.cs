using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YoloAugmentor.Events;

namespace YoloAugmentor.Datasets
{
    public static class DatasetLoader
    {
        private static string[] IMAGE_FILTERS = new string[] { "jpg", "jpeg", "png" };

        private static string _currentImageName = string.Empty;
        public static string CurrentImageName => _currentImageName;

        private static readonly List<YoloImage> _images = new List<YoloImage>();
        public static IReadOnlyCollection<YoloImage> Images = _images.AsReadOnly();

        public static bool IsLoaded { get; private set; }
        public static bool IsLoading { get; private set; }
        public static string DatasetPath { get; private set; }

        public static void DeleteImage(string name)
        {
            if (!IsLoaded)
            {
                return;
            }

            var imageIndex = _images.FindIndex(x => x.Name == name);
            if (imageIndex < 0)
            {
                return;
            }

            var image = _images[imageIndex];
            if (_currentImageName == image.Name)
            {
                if (imageIndex == 0)
                {
                    if (_images.Count <= 1)
                    {
                        _currentImageName = null;
                    }
                    else
                    {
                        var nextImage = GetNextImage();
                        SelectImage(nextImage.Name);
                    }
                }
                else if (imageIndex == _images.Count - 1)
                {
                    if (_images.Count <= 1)
                    {
                        _currentImageName = null;
                    }
                    else
                    {
                        var prevImage = GetPreviousImage();
                        SelectImage(prevImage.Name);
                    }
                }
                else
                {
                    var nextImage = GetNextImage();
                    SelectImage(nextImage.Name);
                }
            }
            image.Delete();
            _images.Remove(image);
        }

        public static YoloImage SelectImage(string name)
        {
            if (!IsLoaded)
            {
                return null;
            }

            var imageIndex = _images.FindIndex(x => x.Name == name);
            if (imageIndex < 0)
            {
                return CurrentImage();
            }

            _currentImageName = name;

            return _images[imageIndex];
        }

        public static YoloImage CurrentImage()
        {
            if (!IsLoaded)
            {
                return null;
            }

            var imageIndex = _images.FindIndex(x => x.Name == _currentImageName);
            if (imageIndex >= 0)
            {
                return _images[imageIndex];
            }
            return null;
        }

        public static YoloImage GetNextImage()
        {
            if (!IsLoaded)
            {
                return null;
            }

            if (string.IsNullOrEmpty(_currentImageName))
            {
                _currentImageName = _images[0].Name;
                return _images[0];
            }
            var imageIndex = _images.FindIndex(x => x.Name == _currentImageName);
            if (imageIndex < _images.Count - 1)
            {
                var nextImage = _images[imageIndex + 1];
                return nextImage;
            }

            return _images[imageIndex];
        }

        public static YoloImage GetPreviousImage()
        {
            if (!IsLoaded)
            {
                return null;
            }

            if (string.IsNullOrEmpty(_currentImageName))
            {
                _currentImageName = _images[0].Name;
                return _images[0];
            }
            var imageIndex = _images.FindIndex(x => x.Name == _currentImageName);
            if (imageIndex > 0)
            {
                var prevImage = _images[imageIndex - 1];
                return prevImage;
            }

            return _images[imageIndex];
        }

        public static void UpdateAugmentations(List<YoloAugmentation> augmentations)
        {
            var currentImage = CurrentImage();
            if (currentImage is null)
            {
                throw new Exception("No image selected");
            }
            currentImage.Augmentations = augmentations;
            currentImage.Save();
        }

        public static async Task LoadAsync(string path)
        {
            await Observable.Start(() =>
            {
                try
                {
                    IsLoaded = false;
                    IsLoading = true;

                    var result = new List<YoloImage>();

                    if (!Directory.Exists(path))
                    {
                        throw new Exception("No folder found");
                    }

                    //Check path
                    var trainImagesPath = $"{path}\\images\\train";
                    var trainLabelsPath = $"{path}\\labels\\train";

                    if (!Directory.Exists(trainImagesPath))
                    {
                        throw new Exception("No image folder found.");
                    }
                    if (!Directory.Exists(trainLabelsPath))
                    {
                        throw new Exception("No label folder found.");
                    }

                    var valImagesPath = $"{path}\\images\\val";
                    var valLabelsPath = $"{path}\\labels\\val";

                    if (!Directory.Exists(valImagesPath))
                    {
                        throw new Exception("No image folder found.");
                    }
                    if (!Directory.Exists(valLabelsPath))
                    {
                        throw new Exception("No label folder found.");
                    }

                    var trainCount = GetFilesFrom(trainImagesPath, IMAGE_FILTERS, false).Length;
                    var valCount = GetFilesFrom(valImagesPath, IMAGE_FILTERS, false).Length;
                    var totalCount = trainCount + valCount;
                    var loadedCount = 0;
                    #region Train set
                    var trainImages = new List<YoloImage>();
                    foreach (var image in Load(trainImagesPath, trainLabelsPath))
                    {
                        trainImages.Add(image);

                        loadedCount++;
                        MessageBus.Current.SendMessage<DatasetLoadingEvent>(new DatasetLoadingEvent
                        {
                            TotalCount = totalCount,
                            LoadedCount = loadedCount
                        });
                    }

                    result.AddRange(trainImages);
                    #endregion

                    //Validation set
                    var valImages = new List<YoloImage>();
                    foreach (var image in Load(valImagesPath, valLabelsPath))
                    {
                        valImages.Add(image);

                        loadedCount++;
                        MessageBus.Current.SendMessage<DatasetLoadingEvent>(new DatasetLoadingEvent
                        {
                            TotalCount = totalCount,
                            LoadedCount = loadedCount
                        });
                    }
                    result.AddRange(valImages);

                    result = result.OrderBy(x => x.DateCreated).ToList();

                    _images.Clear();
                    _images.AddRange(result);


                    IsLoaded = true;
                    IsLoading = false;

                    DatasetPath = path;
                }
                catch (Exception)
                {
                    IsLoading = false;
                    throw;
                }
            }, RxApp.TaskpoolScheduler);
        }

        public static void DeleteByThreshold(string datasetFolder)
        {
            var result = new List<YoloImage>();

            //Format existing dataset to specified width & height.
            var trainImagesPath = $"{datasetFolder}\\images\\train";
            var trainLabelsPath = $"{datasetFolder}\\labels\\train";

            var valImagesPath = $"{datasetFolder}\\images\\val";
            var valLabelsPath = $"{datasetFolder}\\labels\\val";

            var trainImages = new List<YoloImage>();
            foreach (var image in Load(trainImagesPath, trainLabelsPath))
            {
                trainImages.Add(image);
            }

            result.AddRange(trainImages);

            //Validation set
            var valImages = new List<YoloImage>();
            foreach (var image in Load(valImagesPath, valLabelsPath))
            {
                valImages.Add(image);
            }
            result.AddRange(valImages);

            result = result.OrderBy(x => x.DateCreated).ToList();

            var found = false;
            foreach (var yoloImage in result)
            {
                if (yoloImage.Name.Contains("5bc8633e-a4b8-4742-aac6-26b69a316f34"))
                {
                    found = true;
                }
                if (!found)
                {
                    continue;
                }

                //Calculate new augmentations.
                foreach (var aug in yoloImage.Augmentations)
                {
                    if (aug.HeightByImage < (1F / 7F))
                    {
                        File.Delete(yoloImage.ImageFilePath);
                        if (File.Exists(yoloImage.LabelFilePath))
                        {
                            File.Delete(yoloImage.LabelFilePath);
                        }
                        break;
                    }
                }
            }
        }

        public static void FormatDataset(string datasetFolder, int width, int height)
        {
            //Format existing dataset to specified width & height.
            var trainImagesPath = $"{datasetFolder}\\images\\train";
            var trainLabelsPath = $"{datasetFolder}\\labels\\train";

            var valImagesPath = $"{datasetFolder}\\images\\val";
            var valLabelsPath = $"{datasetFolder}\\labels\\val";

            foreach (var image in Load(trainImagesPath, trainLabelsPath))
            {
                FormatImage(image, width, height);
            }

            foreach (var image in Load(valImagesPath, valLabelsPath))
            {
                FormatImage(image, width, height);
            }
        }

        private static void FormatImage(YoloImage image, int width, int height)
        {
            var newAugmentations = new List<YoloAugmentation>();

            using var oldBitmap = (Bitmap)Image.FromFile(image.ImageFilePath);

            if (oldBitmap.Width <= width || oldBitmap.Height <= height)
            {
                return;
            }

            var roi = new Rectangle(
                    (oldBitmap.Width - width) / 2,
                    (oldBitmap.Height - height) / 2,
                    width,
                    height);

            using var newBitmap = CropImage(oldBitmap, roi);

            //Calculate new augmentations.
            foreach (var oldAug in image.Augmentations)
            {
                var oldX = oldBitmap.Width * (oldAug.XCenter - oldAug.WidthByImage / 2);
                var oldY = oldBitmap.Height * (oldAug.YCenter - oldAug.HeightByImage / 2);
                var oldWidth = oldBitmap.Width * oldAug.WidthByImage;
                var oldHeight = oldBitmap.Height * oldAug.HeightByImage;

                if (oldX < roi.Right && oldX + oldWidth > roi.Left &&
                    oldY < roi.Bottom && oldY + oldHeight > roi.Top)
                {
                    //This augmentation is overlap with new ROI.
                    var newX = Math.Max(0, oldX - roi.Left);
                    var newY = Math.Max(0, oldY - roi.Top);
                    var newWidth = Math.Min(roi.Width - 1 - newX, oldWidth);
                    var newHeight = Math.Min(roi.Height - 1 - newY, oldHeight);
                    newAugmentations.Add(new YoloAugmentation
                    {
                        ClassId = oldAug.ClassId,
                        WidthByImage = (float)(newWidth / roi.Width),
                        HeightByImage = (float)(newHeight / roi.Height),
                        XCenter = (float)((newX + newWidth / 2) / roi.Width),
                        YCenter = (float)((newY + newHeight / 2) / roi.Height)
                    });
                }
            }
            oldBitmap.Dispose();

            //Save image.
            newBitmap.Save(image.ImageFilePath, System.Drawing.Imaging.ImageFormat.Png);

            //Save labels.
            if (!image.IsBackgroundImage)
            {
                image.Augmentations = newAugmentations;
                image.Save();
            }
        }

        private static Bitmap CropImage(Bitmap source, Rectangle section)
        {
            var bitmap = new Bitmap(section.Width, section.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.DrawImage(source, 0, 0, section, GraphicsUnit.Pixel);
                return bitmap;
            }
        }

        private static IEnumerable<YoloImage> Load(string imageFolder, string labelFolder)
        {
            var result = new List<YoloImage>();

            //Check path
            if (!Directory.Exists(imageFolder))
            {
                throw new Exception("No image folder found.");
            }
            if (!Directory.Exists(labelFolder))
            {
                throw new Exception("No label folder found.");
            }

            //Get images.
            var imageFiles = GetFilesFrom(imageFolder, IMAGE_FILTERS, false);

            //Load.
            foreach (var imageFile in imageFiles)
            {
                var imageName = Path.GetFileNameWithoutExtension(imageFile);
                var labelFile = $"{labelFolder}\\{imageName}.txt";

                var augmentations = new List<YoloAugmentation>();
                if (File.Exists(labelFile))
                {
                    var augLines = File.ReadAllLines(labelFile);
                    foreach (var augLine in augLines)
                    {
                        var segments = augLine.Split(' ');
                        if (segments.Length < 5)
                        {
                            throw new Exception($"Invalid label:{labelFile}");
                        }
                        augmentations.Add(new YoloAugmentation
                        {
                            ClassId = int.Parse(segments[0]),
                            XCenter = float.Parse(segments[1]),
                            YCenter = float.Parse(segments[2]),
                            WidthByImage = float.Parse(segments[3]),
                            HeightByImage = float.Parse(segments[4]),
                        });
                    }
                }

                var yoloImage = new YoloImage
                {
                    Name = Path.GetFileNameWithoutExtension(imageFile),
                    ImageFilePath = imageFile,
                    LabelFilePath = labelFile,
                    Augmentations = augmentations,
                    DateCreated = File.GetLastWriteTime(imageFile)
                };

                yield return yoloImage;
            }
        }

        private static String[] GetFilesFrom(String searchFolder, String[] filters, bool isRecursive)
        {
            List<String> filesFound = new List<String>();
            var searchOption = isRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var filter in filters)
            {
                filesFound.AddRange(Directory.GetFiles(searchFolder, String.Format("*.{0}", filter), searchOption));
            }
            return filesFound.ToArray();
        }
    }
}
