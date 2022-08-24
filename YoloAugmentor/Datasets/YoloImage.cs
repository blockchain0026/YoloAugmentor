using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Windows.Media.Imaging;

namespace YoloAugmentor.Datasets
{
    public class YoloImage
    {
        public string Name { get; set; }
        public string ImageFilePath { get; set; }
        public string LabelFilePath { get; set; }
        public bool IsBackgroundImage => !Augmentations.Any();
        public DateTime DateCreated { get; set; }

        public List<YoloAugmentation> Augmentations = new List<YoloAugmentation>();


        public void Save()
        {
            var labelText = string.Empty;
            if (Augmentations != null)
            {
                foreach (var annotation in Augmentations)
                {
                    labelText += $"{annotation.ClassId} {annotation.XCenter} {annotation.YCenter} {annotation.WidthByImage} {annotation.HeightByImage}\n";
                }
            }
            File.WriteAllText(LabelFilePath, labelText);
        }

        public void Delete()
        {
            Observable.Start(() =>
            {
                while (true)
                {
                    try
                    {
                        if (File.Exists(ImageFilePath))
                        {
                            File.Delete(ImageFilePath);
                        }
                        if (File.Exists(LabelFilePath))
                        {
                            File.Delete(LabelFilePath);
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        GC.Collect();
                        Thread.Sleep(1000);
                    }
                }
            }, RxApp.TaskpoolScheduler);
        }
    }
}
