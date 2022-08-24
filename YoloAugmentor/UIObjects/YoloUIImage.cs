using DynamicData.Kernel;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YoloAugmentor.Datasets;
using YoloAugmentor.Events;

namespace YoloAugmentor.UIObjects
{
    public class YoloUIImage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private YoloImage _yoloImage;
        public string Name => _yoloImage != null ? _yoloImage.Name : "Error";
        public bool HasTag => _yoloImage != null && !_yoloImage.IsBackgroundImage;

        private ImageSource _thumbnail;

        private bool _isDeleted = false;
        private readonly object _lock = new object();
        public YoloUIImage(YoloImage yoloImage)
        {
            _yoloImage = yoloImage ?? throw new ArgumentNullException(nameof(yoloImage));
        }

        public ImageSource Thumbnail
        {
            get
            {
                if (_thumbnail == null && _yoloImage != null && File.Exists(_yoloImage.ImageFilePath))
                {
                    MessageBus.Current.SendMessage<ReadThumbnailEvent>(new ReadThumbnailEvent
                    {
                        ImageName = this._yoloImage.Name
                    });
                }
                return _thumbnail;
            }
        }

        public static YoloUIImage From(YoloImage yoloImage)
        {
            return new YoloUIImage(yoloImage);
        }

        public void Update(YoloImage yoloImage)
        {
            this._yoloImage = yoloImage;
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(HasTag));
        }

        public void LoadThumbnail()
        {
            lock (_lock)
            {
                if (!_isDeleted && _thumbnail == null && _yoloImage != null && File.Exists(_yoloImage.ImageFilePath))
                {
                    MemoryStream byteStream = new MemoryStream(File.ReadAllBytes(_yoloImage.ImageFilePath));
                    
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = byteStream;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze(); //Important to freeze it, otherwise it will still have minor leaks

                    _thumbnail = image;

                    OnPropertyChanged(nameof(Thumbnail));

                    byteStream.Dispose();
                }
            }
        }

        public void ReleaseThunbnail()
        {
            if (this._thumbnail != null)
            {
                this._thumbnail = null;
            }
        }

        public void Delete()
        {
            lock (_lock)
            {
                this._isDeleted = true;
                this.ReleaseThunbnail();

                GC.Collect();
            }
        }

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
