using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Text;

namespace YoloAugmentor.Styles
{
    public static class ThemeHelper
    {
        public static bool IsDarkTheme()
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();
            return theme.GetBaseTheme() == BaseTheme.Dark;
        }

        public static void ModifyTheme(bool isDarkTheme)
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();

            if (isDarkTheme)
            {
                //Material Design In Toolkit 
                theme.SetBaseTheme(new MaterialDesignDarkTheme());
                theme.SetPrimaryColor(System.Windows.Media.Color.FromArgb(255, 255, 194, 75));
            }
            else
            {
                theme.SetBaseTheme(new MaterialDesignLightTheme());
                theme.SetPrimaryColor(System.Windows.Media.Color.FromArgb(255, 245, 108, 45));
            }
            paletteHelper.SetTheme(theme);
        }

    }
}
