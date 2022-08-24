using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using YoloAugmentor.Datasets;

namespace YoloAugmentor.Projects
{
    public static class ProjectLoader
    {
        private readonly static string EXT = "yaproj";
        public static YAProject LoadedProject { get; private set; }

        public static void SaveProject()
        {
            if (LoadedProject == null)
            {
                CreateProject();
                return;
            }
            if (!Directory.Exists(LoadedProject.ProjectPath))
            {
                LoadedProject.ProjectPath = Directory.GetCurrentDirectory() + $"\\SavedProjects";
            }


            LoadedProject.LastSeenImageName = DatasetLoader.CurrentImageName;
            LoadedProject.Classes = ClassLoader.Classes.ToList();
            LoadedProject.DatasetPath = DatasetLoader.DatasetPath;

            var saveLocation = $"{LoadedProject.ProjectPath}\\{LoadedProject.Name}.{EXT}";

            File.WriteAllText(saveLocation, JsonConvert.SerializeObject(LoadedProject));

            LoadedProject = LoadedProject;
        }

        private static void CreateProject()
        {
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog();
            dialog.FileName = "New"; // Default file name
            dialog.DefaultExt = $".{EXT}"; // Default file extension
            dialog.Filter = $"YAProject (.{EXT})|*.{EXT}"; // Filter files by extension
            dialog.FileOk += (s, e) =>
            {
                SaveFileDialog sv = (s as SaveFileDialog);
                if (Path.GetExtension(sv.FileName).ToLower() != $".{EXT}")
                {
                    e.Cancel = true;
                    MessageBox.Show($"Please omit the extension or use '{EXT}'");
                    return;
                }
            };

            var defaultSaveLocation = Directory.GetCurrentDirectory() + $"\\SavedProjects";
            if (!Directory.Exists(defaultSaveLocation))
            {
                Directory.CreateDirectory(defaultSaveLocation);
            }
            dialog.InitialDirectory = defaultSaveLocation;

            var result = dialog.ShowDialog();
            if (result == true)
            {
                // Save document
                string fileName = dialog.FileName;
                LoadedProject = new YAProject
                {
                    Name = Path.GetFileNameWithoutExtension(fileName),
                    Classes = ClassLoader.Classes.ToList(),
                    ProjectPath = Path.GetDirectoryName(fileName)
                };
                SaveProject();
            }
        }

        public static async Task<bool> TryLoadProjectAsync()
        {
            OpenFileDialog selectFileDialog = new OpenFileDialog();
            var defaultSaveLocation = Directory.GetCurrentDirectory() + $"\\SavedProjects";
            if (!Directory.Exists(defaultSaveLocation))
            {
                Directory.CreateDirectory(defaultSaveLocation);
            }
            selectFileDialog.InitialDirectory = defaultSaveLocation;
            if (selectFileDialog.ShowDialog() == true)
            {
                string selectedFileName = selectFileDialog.FileName;

                var text = File.ReadAllText(selectedFileName);
                var yaproject = JsonConvert.DeserializeObject<YAProject>(text);

                LoadedProject = yaproject;

                ClassLoader.LoadFrom(yaproject);

                if (Directory.Exists(ProjectLoader.LoadedProject.DatasetPath))
                {
                    await DatasetLoader.LoadAsync(ProjectLoader.LoadedProject.DatasetPath);
                    var selectedImage = DatasetLoader.SelectImage(LoadedProject.LastSeenImageName);
                    if (selectedImage is null)
                    {
                        DatasetLoader.GetNextImage();
                    }
                }


                return true;
            }
            return false;
        }

        public static void SaveDatasetPath(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new Exception("No folder found");
            }
            if (LoadedProject == null)
            {
                throw new Exception("No project is loaded");
            }
            LoadedProject.DatasetPath = path;
            File.WriteAllText(LoadedProject.ProjectPath, JsonConvert.SerializeObject(LoadedProject));
        }
    }
}
