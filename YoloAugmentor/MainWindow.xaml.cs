using DynamicData.Kernel;
using Microsoft.WindowsAPICodePack.Dialogs;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using YoloAugmentor.Datasets;
using YoloAugmentor.Events;
using YoloAugmentor.Projects;
using YoloAugmentor.Styles;
using YoloAugmentor.UIObjects;

namespace YoloAugmentor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _showAugName = true;
        private int? _editingTagId = null;

        private ObservableCollection<YoloClass> _classes;
        public ObservableCollection<YoloClass> Classes => _classes;

        private ObservableCollection<YoloUIImage> _images;
        public ObservableCollection<YoloUIImage> Images => _images;

        private readonly Dictionary<int, YoloUIAugmentation> _augmentationDict = new Dictionary<int, YoloUIAugmentation>();

        private Point _newAugStartPoint;
        private Rectangle _newAugRect = null;

        private int? _highlightedAugIndex = null;
        private int? _selectedAugIndex = null;

        private Point _dragingPoint;
        private Rectangle _dragingAugRect = null;

        private bool _resizeXEnabled = false;
        private bool _resizeYEnabled = false;
        private Point _resizingAugStartPoint;
        private Rectangle _resizingAugRect = null;

        private readonly object _imageListLock = new object();
        private DateTime _dateLastReadThumbnail;
        private string _lastReadThumbnailName;
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            ThemeHelper.ModifyTheme(true);

            this._classes = new ObservableCollection<YoloClass>();
            this._images = new ObservableCollection<YoloUIImage>();

            RxApp.TaskpoolScheduler.ScheduleRecurringAction(TimeSpan.FromMilliseconds(25), () =>
            {
                Observable.Start(() =>
                {
                    if (ProjectLoader.LoadedProject == null)
                    {
                        this.leftPanel.IsEnabled = false;
                        this.imageListPanel.IsEnabled = false;
                        this.loadProjectButton.Visibility = Visibility.Visible;
                        this.createProjectButton.Visibility = Visibility.Visible;
                        this.loadDatasetButton.Visibility = Visibility.Collapsed;
                        this.currentImage.Visibility = Visibility.Collapsed;
                        this.currentImage.Source = null;
                        this.nameCanvas.Visibility = Visibility.Collapsed;
                        this.nameCanvas.Children.Clear();
                        this.augmentationCanvas.Visibility = Visibility.Collapsed;
                        this.augmentationCanvas.Children.Clear();
                        this.datasetLoadingTextBlock.Visibility = Visibility.Collapsed;
                        this.datasetLoadingProgressBar.Visibility = Visibility.Collapsed;
                    }
                    else if (DatasetLoader.IsLoading)
                    {
                        this.leftPanel.IsEnabled = false;
                        this.imageListPanel.IsEnabled = false;
                        this.loadProjectButton.Visibility = Visibility.Collapsed;
                        this.createProjectButton.Visibility = Visibility.Collapsed;
                        this.loadDatasetButton.Visibility = Visibility.Collapsed;
                        this.currentImage.Visibility = Visibility.Collapsed;
                        this.currentImage.Source = null;
                        this.nameCanvas.Visibility = Visibility.Collapsed;
                        this.nameCanvas.Children.Clear();
                        this.augmentationCanvas.Visibility = Visibility.Collapsed;
                        this.augmentationCanvas.Children.Clear();
                        this.datasetLoadingTextBlock.Visibility = Visibility.Visible;
                        this.datasetLoadingProgressBar.Visibility = Visibility.Visible;
                    }
                    else if (!DatasetLoader.IsLoaded)
                    {
                        this.leftPanel.IsEnabled = false;
                        this.imageListPanel.IsEnabled = false;
                        this.loadProjectButton.Visibility = Visibility.Collapsed;
                        this.createProjectButton.Visibility = Visibility.Collapsed;
                        this.loadDatasetButton.Visibility = Visibility.Visible;
                        this.currentImage.Visibility = Visibility.Collapsed;
                        this.currentImage.Source = null;
                        this.nameCanvas.Visibility = Visibility.Collapsed;
                        this.nameCanvas.Children.Clear();
                        this.augmentationCanvas.Visibility = Visibility.Collapsed;
                        this.augmentationCanvas.Children.Clear();
                        this.datasetLoadingTextBlock.Visibility = Visibility.Collapsed;
                        this.datasetLoadingProgressBar.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        this.leftPanel.IsEnabled = true;
                        this.imageListPanel.IsEnabled = true;
                        this.loadProjectButton.Visibility = Visibility.Collapsed;
                        this.createProjectButton.Visibility = Visibility.Collapsed;
                        this.loadDatasetButton.Visibility = Visibility.Collapsed;
                        this.nameCanvas.Visibility =
                            !this._showAugName || this._dragingAugRect != null || this._resizingAugRect != null ?
                            Visibility.Collapsed : Visibility.Visible;
                        this.augmentationCanvas.Visibility = Visibility.Visible;
                        this.currentImage.Visibility = Visibility.Visible;
                        this.datasetLoadingTextBlock.Visibility = Visibility.Collapsed;
                        this.datasetLoadingProgressBar.Visibility = Visibility.Collapsed;
                    }
                }, RxApp.MainThreadScheduler);

            });

            MessageBus.Current.Listen<DatasetLoadingEvent>()
                .Do(args =>
                {
                    Observable.Start(() =>
                    {
                        this.datasetLoadingProgressBar.Value = (int)((double)args.LoadedCount / args.TotalCount * 100);
                        this.datasetLoadingTextBlock.Text = $"{args.LoadedCount} / {args.TotalCount}";
                    }, RxApp.MainThreadScheduler);
                })
                .Subscribe();

            MessageBus.Current.Listen<ReadThumbnailEvent>()
                .Do(args =>
                {
                    lock (_imageListLock)
                    {
                        _dateLastReadThumbnail = DateTime.UtcNow;
                        _lastReadThumbnailName = args.ImageName;
                    }
                })
                .Subscribe();

            RxApp.TaskpoolScheduler.ScheduleRecurringAction(TimeSpan.FromMilliseconds(200), () =>
            {
                if (!DatasetLoader.IsLoaded)
                {
                    return;
                }

                lock (_imageListLock)
                {
                    if (!string.IsNullOrEmpty(this._lastReadThumbnailName))
                    {
                        var image = this._images.FirstOrDefault(m => m.Name == this._lastReadThumbnailName);
                        var index = this._images.IndexOf(image);
                        if (index >= 0)
                        {
                            var startIndex = Math.Max(0, index - 7);
                            var endIndex = Math.Min(index + 7, this._images.Count - 1);

                            for (int i = startIndex; i <= endIndex; i++)
                            {
                                this._images[i].LoadThumbnail();
                            }
                        }
                    }
                }
            });

            RxApp.TaskpoolScheduler.ScheduleRecurringAction(TimeSpan.FromSeconds(1), () =>
            {
                if (!DatasetLoader.IsLoaded)
                {
                    return;
                }

                lock (_imageListLock)
                {
                    if (!string.IsNullOrEmpty(this._lastReadThumbnailName))
                    {
                        var image = this._images.FirstOrDefault(m => m.Name == this._lastReadThumbnailName);
                        var index = this._images.IndexOf(image);

                        var startIndex = Math.Max(0, index - 10);
                        var endIndex = Math.Min(index + 10, this._images.Count - 1);
                        for (int i = 0; i < this._images.Count; i++)
                        {
                            if (i < startIndex || i > endIndex)
                            {
                                this._images[i].ReleaseThunbnail();
                            }
                        }
                    }
                }
            });


            //DatasetLoader.FormatDataset(
            //    @"D:\MachineLearning\Yolo\Projects\ApexMLYolo\datasets\apexml",
            //    480,
            //    480);


            //DatasetLoader.DeleteByThreshold(@"D:\MachineLearning\Augmentations\Captures\apexml_old_480");
        }

        #region Sizes
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (!DatasetLoader.IsLoaded)
                {
                    return;
                }

                var currentImage = DatasetLoader.CurrentImage();
                if (currentImage != null)
                {
                    this.DisplayImage(currentImage);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void augmentationCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (!DatasetLoader.IsLoaded)
                {
                    return;
                }
                var currentImage = DatasetLoader.CurrentImage();
                if (currentImage != null)
                {
                    this.DisplayImage(currentImage);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        #endregion

        #region Datasets
        private async void loadDatasetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CommonOpenFileDialog dialog = new CommonOpenFileDialog();
                dialog.RestoreDirectory = true;
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    await DatasetLoader.LoadAsync(dialog.FileName);
                    this.DisplayImage(DatasetLoader.GetNextImage());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void DeleteSelectedImage()
        {
            if (!DatasetLoader.IsLoaded || ProjectLoader.LoadedProject == null)
            {
                return;
            }

            var selectedUIImage = this.imageListBox.SelectedItem as YoloUIImage;
            if (selectedUIImage is null)
            {
                return;
            }
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show("Are you sure to delete this image?", "Delete Confirmation", System.Windows.MessageBoxButton.YesNo);
            if (messageBoxResult != MessageBoxResult.Yes)
            {
                return;
            }
            selectedUIImage.Delete();
            this._images.Remove(selectedUIImage);
            this.currentImage.Source = null;
            this._augmentationDict.Clear();
            this.augmentationCanvas.Children.Clear();
            this.nameCanvas.Children.Clear();

            DatasetLoader.DeleteImage(selectedUIImage.Name);

            var newImage = DatasetLoader.CurrentImage();
            if (newImage != null)
            {
                var uiImage = YoloUIImage.From(newImage);
                this.imageListBox.SelectedItem = uiImage;
            }
        }

        #endregion

        #region Tags
        private void addTagButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.tagIdTextBox.Text =
                    ClassLoader.Classes.Any() ? (ClassLoader.Classes.Max(t => t.ClassId) + 1).ToString() : "0";
                this.tagIdTextBox.IsReadOnly = false;
                this.tagNameTextBox.Text = string.Empty;

                this._editingTagId = null;
                this.addTagDialog.IsOpen = true;
                this.tagNameTextBox.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void addTagConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this._editingTagId != null)
                {
                    ClassLoader.EditClass((int)this._editingTagId, this.tagNameTextBox.Text);

                    this._editingTagId = null;
                    this.addTagDialog.IsOpen = false;
                }
                else
                {
                    var tagId = int.Parse(this.tagIdTextBox.Text);
                    ClassLoader.AddClass(tagId, this.tagNameTextBox.Text);
                    this.addTagDialog.IsOpen = false;
                }

                RefreshTags();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void editTagButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedTag = this.tagListBox.SelectedItem as YoloClass;
                if (selectedTag is null)
                {
                    return;
                }
                this.tagNameTextBox.Text = selectedTag.Name;
                this.tagIdTextBox.Text = selectedTag.ClassId.ToString();
                this.tagIdTextBox.IsReadOnly = true;

                this._editingTagId = selectedTag.ClassId;
                this.addTagDialog.IsOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void changeTagColorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedTag = this.tagListBox.SelectedItem as YoloClass;
                if (selectedTag is null)
                {
                    return;
                }
                ClassLoader.ChangeClassColor(selectedTag.ClassId);
                this.RefreshTags();
                this.CorrectNameLabels();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void RefreshTags()
        {
            this._classes.Clear();
            foreach (var augClass in ClassLoader.Classes)
            {
                this._classes.Add(augClass);

                for (int i = 0; i < this.augmentationCanvas.Children.Count; i++)
                {
                    var augBox = this.augmentationCanvas.Children[i] as Rectangle;

                    if (augBox.Tag != null)
                    {
                        this.ChangeAugColorToNormal((int)augBox.Tag, true);
                    }
                }
            }
        }

        private void removeTagButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedTag = this.tagListBox.SelectedItem as YoloClass;
                if (selectedTag is null)
                {
                    throw new Exception("No tag selected");
                }

                ClassLoader.RemoveClass(selectedTag.ClassId);

                RefreshTags();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        #endregion

        #region Projects
        private void saveProjectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProjectLoader.SaveProject();
                MessageBox.Show("儲存成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void loadProjectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (await ProjectLoader.TryLoadProjectAsync())
                {
                    lock (_imageListLock)
                    {
                        this._classes.Clear();
                        foreach (var tag in ClassLoader.Classes)
                        {
                            this._classes.Add(tag);
                        }

                        this._images.Clear();
                        foreach (var image in DatasetLoader.Images)
                        {
                            this._images.Add(YoloUIImage.From(image));
                        }

                        var currentImage = DatasetLoader.CurrentImage();
                        if (currentImage != null)
                        {
                            var uiImage = this._images.FirstOrDefault(i => i.Name == currentImage.Name);
                            if (uiImage != null)
                            {
                                this.imageListBox.SelectedItem = uiImage;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        #endregion

        #region Augmentations
        private void DeleteCurrentAugmentation()
        {
            if (this._selectedAugIndex == null)
            {
                return;
            }
            int currentAugIndex = (int)this._selectedAugIndex;
            for (int i = 0; i < this.augmentationCanvas.Children.Count; i++)
            {
                var augBox = this.augmentationCanvas.Children[i] as Rectangle;

                if (augBox.Tag != null && (int)augBox.Tag == currentAugIndex)
                {
                    this.augmentationCanvas.Children.Remove(augBox);
                }
            }
            for (int i = 0; i < this.nameCanvas.Children.Count; i++)
            {
                var augNameTextBlock = this.nameCanvas.Children[i] as TextBlock;

                if (augNameTextBlock.Tag != null && (int)augNameTextBlock.Tag == currentAugIndex)
                {
                    this.nameCanvas.Children.Remove(augNameTextBlock);
                }
            }

            this._augmentationDict.Remove(currentAugIndex);
            this._dragingAugRect = null;
            this._selectedAugIndex = null;
        }

        private void augmentationCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (_highlightedAugIndex != null && _dragingAugRect == null && _resizingAugRect == null)
                {
                    foreach (var augElement in augmentationCanvas.Children)
                    {
                        var augBox = augElement as Rectangle;
                        if (augBox.Tag != null)
                        {
                            var augmentation = _augmentationDict[(int)augBox.Tag];
                            var augClass = this._classes.FirstOrDefault(t => t.ClassId == augmentation.ClassId);
                            if ((int)augBox.Tag == (int)_highlightedAugIndex)
                            {
                                var pos = e.GetPosition(augBox);
                                var augBoxX = Canvas.GetLeft(augBox);
                                var augBoxY = Canvas.GetTop(augBox);

                                var xScaleEnabled = pos.X < 20 || pos.X > augBox.Width - 20;
                                var yScaleEnabled = pos.Y < 20 || pos.Y > augBox.Height - 20;
                                if (xScaleEnabled || yScaleEnabled)
                                {
                                    this._resizingAugRect = augBox;
                                    this._resizeXEnabled = xScaleEnabled;
                                    this._resizeYEnabled = yScaleEnabled;

                                    if (pos.X < 20)
                                    {
                                        if (pos.Y < 20)
                                        {
                                            _resizingAugStartPoint = new Point(augBoxX + augBox.Width, augBoxY + augBox.Height);
                                        }
                                        else
                                        {
                                            _resizingAugStartPoint = new Point(augBoxX + augBox.Width, augBoxY);
                                        }
                                    }
                                    else
                                    {
                                        if (pos.Y < 20)
                                        {
                                            _resizingAugStartPoint = new Point(augBoxX, augBoxY + augBox.Height);
                                        }
                                        else
                                        {
                                            _resizingAugStartPoint = new Point(augBoxX, augBoxY);
                                        }
                                    }


                                }

                                this.ChangeAugColorToHighlight(augmentation.Index, false);
                            }
                            else
                            {
                                this.ChangeAugColorToNormal(augmentation.Index, false);
                            }
                        }
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void augmentationCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (_highlightedAugIndex != null)
                {
                    foreach (var augElement in augmentationCanvas.Children)
                    {
                        var augBox = augElement as Rectangle;
                        if (augBox.Tag != null)
                        {
                            var augmentation = _augmentationDict[(int)augBox.Tag];
                            var augClass = this._classes.FirstOrDefault(t => t.ClassId == augmentation.ClassId);
                            if ((int)augBox.Tag == (int)_highlightedAugIndex)
                            {
                                _dragingAugRect = augBox;
                                _selectedAugIndex = (int)augBox.Tag;
                                _dragingPoint = e.GetPosition(augBox);

                                this.ChangeAugColorToHighlight(augmentation.Index, false);
                            }
                            else
                            {
                                this.ChangeAugColorToNormal(augmentation.Index, false);
                            }
                        }
                    }
                    return;
                }
                if (this._selectedAugIndex != null)
                {
                    this.ChangeAugColorToNormal((int)this._selectedAugIndex, false);
                    this._selectedAugIndex = null;
                }

                _newAugStartPoint = e.GetPosition(augmentationCanvas);

                var isNew = _newAugRect == null;
                if (isNew)
                {
                    _newAugRect = new Rectangle
                    {
                        Stroke = Brushes.Gray,
                        StrokeThickness = 3,
                        Tag = _augmentationDict.Keys.Any() ? _augmentationDict.Keys.Max() + 1 : 0 //New index
                    };
                }

                Canvas.SetLeft(_newAugRect, _newAugStartPoint.X);
                Canvas.SetTop(_newAugRect, _newAugStartPoint.Y);

                if (isNew)
                {
                    augmentationCanvas.Children.Add(_newAugRect);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void augmentationCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                _highlightedAugIndex = null;
                _dragingPoint = default;
                _dragingAugRect = null;

                _resizingAugRect = null;
                _resizingAugStartPoint = default;
                this._resizeXEnabled = false;
                this._resizeYEnabled = false;

                if (_newAugRect != null)
                {
                    if (_newAugRect.ActualWidth * _newAugRect.ActualHeight < 100) //Small than 10*10
                    {
                        this.augmentationCanvas.Children.Remove(_newAugRect);
                    }
                    else
                    {
                        var selectedYoloTag = this.tagListBox.SelectedItem as YoloClass;
                        var newAugIndex = (int)_newAugRect.Tag;

                        _augmentationDict.Add((int)_newAugRect.Tag, new YoloUIAugmentation
                        {
                            ClassId = selectedYoloTag != null ? selectedYoloTag.ClassId : -1,
                            Index = newAugIndex
                        });
                        this._selectedAugIndex = newAugIndex;

                        this.ChangeAugColorToHighlight(newAugIndex, true);
                    }
                }
                _newAugRect = null;
                this.CorrectNameLabels();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void augmentationCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                var pos = e.GetPosition(augmentationCanvas);
                if (e.LeftButton == MouseButtonState.Released && e.RightButton == MouseButtonState.Released)
                {
                    //Highlight when hover on augmentation box.
                    Rectangle highlightAugBox = null;
                    foreach (var augmentation in this.augmentationCanvas.Children)
                    {
                        var augBox = augmentation as Rectangle;
                        var augBoxX = Canvas.GetLeft(augBox);
                        var augBoxY = Canvas.GetTop(augBox);
                        if ((int)augBox.Tag != this._selectedAugIndex)
                        {
                            augBox.StrokeThickness = 3;
                        }

                        if (pos.X >= augBoxX
                            && pos.X <= augBoxX + augBox.ActualWidth
                            && pos.Y >= augBoxY
                            && pos.Y <= augBoxY + augBox.ActualHeight)
                        {
                            if (highlightAugBox != null
                                && highlightAugBox.Width * highlightAugBox.Height < augBox.Width * augBox.Height)
                            {
                                continue;
                            }

                            highlightAugBox = augBox;
                        }
                    }
                    if (highlightAugBox != null)
                    {
                        highlightAugBox.StrokeThickness = 5;
                        this.Cursor = Cursors.Hand;
                        if (highlightAugBox.Tag != null)
                        {
                            _highlightedAugIndex = (int)highlightAugBox.Tag;
                        }
                        else
                        {
                            _highlightedAugIndex = null;
                        }
                    }
                    else
                    {
                        _highlightedAugIndex = null;
                        this.Cursor = Cursors.Arrow;
                    }
                    return;
                }
                if (_dragingAugRect != null)
                {
                    var newX = pos.X - _dragingPoint.X;
                    var newY = pos.Y - _dragingPoint.Y;
                    if (newX < 0
                        || newX + _dragingAugRect.ActualWidth > augmentationCanvas.ActualWidth
                        || newY < 0
                        || newY + _dragingAugRect.ActualHeight > augmentationCanvas.ActualHeight)
                    {
                        _dragingPoint = e.GetPosition(_dragingAugRect);
                        return;
                    }
                    Canvas.SetLeft(_dragingAugRect, newX);
                    Canvas.SetTop(_dragingAugRect, newY);
                    return;
                }
                if (_newAugRect != null)
                {
                    var x = Math.Min(pos.X, _newAugStartPoint.X);
                    var y = Math.Min(pos.Y, _newAugStartPoint.Y);

                    var w = Math.Max(pos.X, _newAugStartPoint.X) - x;
                    var h = Math.Max(pos.Y, _newAugStartPoint.Y) - y;

                    _newAugRect.Width = w;
                    _newAugRect.Height = h;

                    Canvas.SetLeft(_newAugRect, x);
                    Canvas.SetTop(_newAugRect, y);
                }
                if (_resizingAugRect != null)
                {
                    if (this._resizeXEnabled)
                    {
                        var x = Math.Min(pos.X, _resizingAugStartPoint.X);
                        var w = Math.Max(pos.X, _resizingAugStartPoint.X) - x;
                        _resizingAugRect.Width = w;

                        Canvas.SetLeft(_resizingAugRect, x);
                    }
                    if (this._resizeYEnabled)
                    {
                        var y = Math.Min(pos.Y, _resizingAugStartPoint.Y);
                        var h = Math.Max(pos.Y, _resizingAugStartPoint.Y) - y;
                        _resizingAugRect.Height = h;

                        Canvas.SetTop(_resizingAugRect, y);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void CorrectNameLabels()
        {
            for (int i = 0; i < this.augmentationCanvas.Children.Count; i++)
            {
                var augBox = this.augmentationCanvas.Children[i] as Rectangle;

                if (augBox.Tag != null)
                {
                    var augmentationIndex = (int)augBox.Tag;
                    for (int a = 0; a < this.nameCanvas.Children.Count; a++)
                    {
                        var nameTextBlock = this.nameCanvas.Children[a] as TextBlock;
                        if (nameTextBlock != null && nameTextBlock.Tag != null && (int)nameTextBlock.Tag == augmentationIndex)
                        {
                            Canvas.SetLeft(nameTextBlock, Canvas.GetLeft(augBox));
                            Canvas.SetTop(nameTextBlock, Canvas.GetTop(augBox));
                        }
                    }
                }
            }
        }

        private void ChangeAugColorToHighlight(int augmentationIndex, bool createNameLabel)
        {
            var augmentation = _augmentationDict[augmentationIndex];
            var augClass = this._classes.FirstOrDefault(t => t.ClassId == augmentation.ClassId);


            for (int i = 0; i < this.augmentationCanvas.Children.Count; i++)
            {
                var augBox = this.augmentationCanvas.Children[i] as Rectangle;

                if (augBox.Tag != null && (int)augBox.Tag == augmentationIndex)
                {
                    augBox.Stroke = new SolidColorBrush(
                        augClass != null ?
                        Color.FromArgb(255, augClass.Color.R, augClass.Color.G, augClass.Color.B)
                        : Colors.Gray);
                    augBox.Fill = new SolidColorBrush(
                        augClass != null ?
                        Color.FromArgb(75, augClass.Color.R, augClass.Color.G, augClass.Color.B)
                        : Colors.Transparent);
                    augBox.StrokeThickness = 5;

                    //Name
                    if (createNameLabel && augClass != null)
                    {
                        TextBlock nameTextBlock = new TextBlock();

                        nameTextBlock.Text = augClass.Name;
                        nameTextBlock.Padding = new Thickness(5, 2, 5, 2);

                        nameTextBlock.Foreground = new SolidColorBrush(Colors.AliceBlue);
                        nameTextBlock.Background = new SolidColorBrush(augClass.Color);
                        Canvas.SetLeft(nameTextBlock, Canvas.GetLeft(augBox));
                        Canvas.SetTop(nameTextBlock, Canvas.GetTop(augBox));
                        nameTextBlock.Tag = augmentationIndex;
                        nameCanvas.Children.Add(nameTextBlock);
                    }
                }
            }
        }

        private void ChangeAugColorToNormal(int augmentationIndex, bool createNameLabel)
        {
            var augmentation = _augmentationDict[augmentationIndex];
            var augClass = this._classes.FirstOrDefault(t => t.ClassId == augmentation.ClassId);


            for (int i = 0; i < this.augmentationCanvas.Children.Count; i++)
            {
                var augBox = this.augmentationCanvas.Children[i] as Rectangle;

                if (augBox.Tag != null && (int)augBox.Tag == augmentationIndex)
                {
                    augBox.Stroke = new SolidColorBrush(
                        augClass != null ?
                        Color.FromArgb(180, augClass.Color.R, augClass.Color.G, augClass.Color.B)
                        : Colors.Gray);
                    augBox.Fill = new SolidColorBrush(
                        augClass != null ?
                        Color.FromArgb(25, augClass.Color.R, augClass.Color.G, augClass.Color.B)
                        : Colors.Transparent);
                    augBox.StrokeThickness = 3;

                    //Name
                    if (createNameLabel && augClass != null)
                    {
                        TextBlock nameTextBlock = new TextBlock();

                        nameTextBlock.Text = augClass.Name;
                        nameTextBlock.Padding = new Thickness(5, 2, 5, 2);
                        nameTextBlock.Foreground = new SolidColorBrush(Colors.AliceBlue);
                        nameTextBlock.Background = new SolidColorBrush(augClass.Color);
                        Canvas.SetLeft(nameTextBlock, Canvas.GetLeft(augBox));
                        Canvas.SetTop(nameTextBlock, Canvas.GetTop(augBox));
                        nameTextBlock.Tag = augmentationIndex;
                        nameCanvas.Children.Add(nameTextBlock);
                    }
                }
            }
        }

        private void SaveAugmentations()
        {
            if (this._augmentationDict.Any(a => a.Value.ClassId < 0))
            {
                throw new Exception("There are augmentations without class.");
            }
            var yoloAugmentations = new List<YoloAugmentation>();
            foreach (var uiAugmentation in this._augmentationDict.Values)
            {
                foreach (var augElement in augmentationCanvas.Children)
                {
                    var augBox = augElement as Rectangle;
                    if (augBox is null || augBox.Tag is null)
                    {
                        continue;
                    }
                    var augmentationIndex = (int)augBox.Tag;
                    if (augmentationIndex == uiAugmentation.Index)
                    {
                        var x = Canvas.GetLeft(augBox);
                        var y = Canvas.GetTop(augBox);

                        var yoloAugmentation = new YoloAugmentation
                        {
                            ClassId = uiAugmentation.ClassId,
                            WidthByImage = (float)(augBox.ActualWidth / augmentationCanvas.ActualWidth),
                            HeightByImage = (float)(augBox.ActualHeight / augmentationCanvas.ActualHeight),
                            XCenter = (float)((x + augBox.ActualWidth / 2) / augmentationCanvas.ActualWidth),
                            YCenter = (float)((y + augBox.ActualHeight / 2) / augmentationCanvas.ActualHeight)
                        };
                        yoloAugmentations.Add(yoloAugmentation);
                        break;
                    }

                }
            }
            DatasetLoader.UpdateAugmentations(yoloAugmentations);
            var currentImage = DatasetLoader.CurrentImage();
            var uiImage = this._images.FirstOrDefault(i => i.Name == currentImage.Name);
            if (uiImage != null)
            {
                uiImage.Update(currentImage);
            }
        }
        #endregion

        private void DisplayImage(YoloImage yoloImage)
        {
            if (yoloImage == null)
            {
                return;
            }


            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(yoloImage.ImageFilePath);
            bitmap.EndInit();
            bitmap.Freeze(); //Important to freeze it, otherwise it will still have minor leaks
            this.currentImage.Source = bitmap;
            this.currentImage.UpdateLayout();

            augmentationCanvas.Children.Clear();
            nameCanvas.Children.Clear();
            if (yoloImage.Augmentations is null || !yoloImage.Augmentations.Any())
            {
                return;
            }


            //Draw augmentations.
            _augmentationDict.Clear();
            for (int i = 0; i < yoloImage.Augmentations.Count(); i++)
            {
                //Bounding Box
                var augmentation = yoloImage.Augmentations[i];
                var augClass = this._classes.FirstOrDefault(c => c.ClassId == augmentation.ClassId);
                var displayImgWidth = this.currentImage.ActualWidth;
                var displayImgHeight = this.currentImage.ActualHeight;

                System.Windows.Shapes.Rectangle rectangle;
                rectangle = new System.Windows.Shapes.Rectangle();

                rectangle.Stroke = new SolidColorBrush(augClass != null ? augClass.Color : Colors.Gray);
                rectangle.StrokeThickness = 3;
                rectangle.Fill = new SolidColorBrush(Colors.Transparent);
                rectangle.Width = displayImgWidth * augmentation.WidthByImage;
                rectangle.Height = displayImgHeight * augmentation.HeightByImage;
                rectangle.Tag = i;
                Canvas.SetLeft(rectangle, displayImgWidth * (augmentation.XCenter - augmentation.WidthByImage / 2));
                Canvas.SetTop(rectangle, displayImgHeight * (augmentation.YCenter - augmentation.HeightByImage / 2));
                Debug.WriteLine(rectangle.Tag);

                augmentationCanvas.Children.Add(rectangle);
                _augmentationDict.Add(i, new YoloUIAugmentation
                {
                    ClassId = augmentation.ClassId,
                    Index = i
                });

                this.ChangeAugColorToNormal(i, true);
            }

            _highlightedAugIndex = null;
            _dragingPoint = default;
            _dragingAugRect = null;
            _selectedAugIndex = null;
            _newAugRect = null;
        }


        private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (!DatasetLoader.Images.Any())
                {
                    return;
                }
                if (this._dragingAugRect != null || this._resizingAugRect != null || this._newAugRect != null)
                {
                    return;
                }
                switch (e.Key)
                {
                    case Key.Left:
                        if (this._selectedAugIndex != null)
                        {
                            for (int i = 0; i < this.augmentationCanvas.Children.Count; i++)
                            {
                                var augBox = this.augmentationCanvas.Children[i] as Rectangle;
                                if (augBox != null && augBox.Tag != null && (int)augBox.Tag == this._selectedAugIndex)
                                {
                                    var newX = Canvas.GetLeft(augBox) - 1;
                                    var newY = Canvas.GetTop(augBox);
                                    if (newX < 0
                                        || newX + augBox.ActualWidth > augmentationCanvas.ActualWidth - 1
                                        || newY < 0
                                        || newY + augBox.ActualHeight > augmentationCanvas.ActualHeight - 1)
                                    {
                                        return;
                                    }
                                    Canvas.SetLeft(augBox, newX);
                                    Canvas.SetTop(augBox, newY);
                                    return;
                                }
                            }
                        }
                        else
                        {
                            var image = DatasetLoader.GetPreviousImage();
                            var thumbnail = this._images.FirstOrDefault(i => i.Name == image.Name);
                            if (thumbnail != null)
                            {
                                this.imageListBox.SelectedItem = thumbnail;
                            }
                        }
                        break;
                    case Key.Right:
                        if (this._selectedAugIndex != null)
                        {
                            for (int i = 0; i < this.augmentationCanvas.Children.Count; i++)
                            {
                                var augBox = this.augmentationCanvas.Children[i] as Rectangle;
                                if (augBox != null && augBox.Tag != null && (int)augBox.Tag == this._selectedAugIndex)
                                {
                                    var newX = Canvas.GetLeft(augBox) + 1;
                                    var newY = Canvas.GetTop(augBox);
                                    if (newX < 0
                                        || newX + augBox.ActualWidth > augmentationCanvas.ActualWidth - 1
                                        || newY < 0
                                        || newY + augBox.ActualHeight > augmentationCanvas.ActualHeight - 1)
                                    {
                                        return;
                                    }
                                    Canvas.SetLeft(augBox, newX);
                                    Canvas.SetTop(augBox, newY);
                                    return;
                                }
                            }
                        }
                        else
                        {
                            var image = DatasetLoader.GetNextImage();
                            var thumbnail = this._images.FirstOrDefault(i => i.Name == image.Name);
                            if (thumbnail != null)
                            {
                                this.imageListBox.SelectedItem = thumbnail;
                            }
                        }
                        break;
                    case Key.Up:
                        if (this._selectedAugIndex != null)
                        {
                            for (int i = 0; i < this.augmentationCanvas.Children.Count; i++)
                            {
                                var augBox = this.augmentationCanvas.Children[i] as Rectangle;
                                if (augBox != null && augBox.Tag != null && (int)augBox.Tag == this._selectedAugIndex)
                                {
                                    var newX = Canvas.GetLeft(augBox);
                                    var newY = Canvas.GetTop(augBox) - 1;
                                    if (newX < 0
                                        || newX + augBox.ActualWidth > augmentationCanvas.ActualWidth - 1
                                        || newY < 0
                                        || newY + augBox.ActualHeight > augmentationCanvas.ActualHeight - 1)
                                    {
                                        return;
                                    }
                                    Canvas.SetLeft(augBox, newX);
                                    Canvas.SetTop(augBox, newY);
                                    return;
                                }
                            }
                        }
                        break;
                    case Key.Down:
                        if (this._selectedAugIndex != null)
                        {
                            for (int i = 0; i < this.augmentationCanvas.Children.Count; i++)
                            {
                                var augBox = this.augmentationCanvas.Children[i] as Rectangle;
                                if (augBox != null && augBox.Tag != null && (int)augBox.Tag == this._selectedAugIndex)
                                {
                                    var newX = Canvas.GetLeft(augBox);
                                    var newY = Canvas.GetTop(augBox) + 1;
                                    if (newX < 0
                                        || newX + augBox.ActualWidth > augmentationCanvas.ActualWidth - 1
                                        || newY < 0
                                        || newY + augBox.ActualHeight > augmentationCanvas.ActualHeight - 1)
                                    {
                                        return;
                                    }
                                    Canvas.SetLeft(augBox, newX);
                                    Canvas.SetTop(augBox, newY);
                                    return;
                                }
                            }
                        }
                        break;
                    case Key.Delete:
                        if (this._selectedAugIndex != null)
                        {
                            this.DeleteCurrentAugmentation();
                        }
                        else
                        {
                            this.DeleteSelectedImage();
                        }
                        break;
                    case Key.Z:
                        this._showAugName = !this._showAugName;
                        break;
                    default:
                        return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void classListItem_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is StackPanel classListItem && classListItem.Tag != null && this._selectedAugIndex != null)
                {
                    var classId = (int)classListItem.Tag;
                    var selectedAugmentation = this._augmentationDict[(int)_selectedAugIndex];
                    selectedAugmentation.ClassId = classId;
                    this.ChangeAugColorToHighlight(selectedAugmentation.Index, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void tagListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (this._selectedAugIndex != null)
                {
                    var selectedYoloClassItem = this.tagListBox.SelectedItem as YoloClass;
                    if (selectedYoloClassItem != null)
                    {
                        var classId = selectedYoloClassItem.ClassId;
                        var selectedAugmentation = this._augmentationDict[(int)_selectedAugIndex];
                        selectedAugmentation.ClassId = classId;
                        this.ChangeAugColorToHighlight(selectedAugmentation.Index, true);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void imageListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true; //Prevent keyboard navigation of image list.
        }

        private void imageListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var uiImage = this.imageListBox.SelectedItem as YoloUIImage;

                if (uiImage != null)
                {
                    if (this.currentImage.ActualWidth != 0 && this.currentImage.ActualHeight != 0)
                    {
                        this.SaveAugmentations();
                    }
                    var selectedImage = DatasetLoader.SelectImage(uiImage.Name);
                    this.DisplayImage(selectedImage);
                    ProjectLoader.SaveProject();

                    var listBoxItem = (ListBoxItem)imageListBox
                    .ItemContainerGenerator
                    .ContainerFromItem(this.imageListBox.SelectedItem);
                    if (listBoxItem != null)
                    {
                        listBoxItem.FocusVisualStyle = null;
                        listBoxItem.Focus();
                    }

                    this.imageListBox.ScrollIntoView(uiImage);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void openDatasetFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentImage = DatasetLoader.CurrentImage();
                if (currentImage != null)
                {
                    var startInfo = new ProcessStartInfo("explorer.exe", "/select,\"" + currentImage.ImageFilePath + "\"");
                    Process.Start(startInfo);
                }
                else
                {
                    Process.Start("explorer.exe", ProjectLoader.LoadedProject.DatasetPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}