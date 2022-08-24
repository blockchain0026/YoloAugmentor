using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YoloAugmentor.Projects;

namespace YoloAugmentor.Datasets
{
    public static class ClassLoader
    {
        private static readonly List<YoloClass> _classes = new List<YoloClass>();
        public static IReadOnlyCollection<YoloClass> Classes = _classes.AsReadOnly();

        public static void LoadFrom(YAProject yAProject)
        {
            if (yAProject is null || yAProject.Classes == null || !yAProject.Classes.Any())
            {
                return;
            }
            _classes.Clear();
            _classes.AddRange(yAProject.Classes);
        }

        public static void AddClass(int classId, string className)
        {
            if (_classes.Any(t => t.ClassId == classId))
            {
                throw new Exception("Duplicated tag ID");
            }

            var tag = YoloClass.From(classId, className);
            _classes.Add(tag);
            _classes.OrderBy(a => a.ClassId);
        }

        public static void EditClass(int classId, string className)
        {
            var tag = _classes.First(t => t.ClassId == classId);

            tag.ChangeName(className);
        }
        public static void ChangeClassColor(int classId)
        {
            var tag = _classes.First(t => t.ClassId == classId);

            tag.ChangeColor();
        }

        public static void RemoveClass(int classId)
        {
            var tag = _classes.First(t => t.ClassId == classId);
            _classes.Remove(tag);
        }
    }
}
