using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
using System.Reflection;
using Microsoft.Win32;

namespace Colorizator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string fullCurvesPattern = @"-i ""{0}"" -vf curves=r='0.{1}/0.{2}':g='0.{3}/0.{4}':b='0.{5}/0.{6}' ""{7}"" -y";
        private string curvesModePattern = @"curves=r='0.{1}/0.{2}':g='0.{3}/0.{4}':b='0.{5}/0.{6}'";
        private string directoryPath = "";
        private string initialImagePath = "";
        private string currentState = "";
        public InitialSampleWindow InitialSample;
        public InitialSampleWindow ResultSample;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ConfigureViewWindows();
            directoryPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            initialImagePath = directoryPath + @"\start.jpg";
            this.StartIMG.Source = new BitmapImage(new Uri(initialImagePath));
            var filtersNames = Directory.GetFiles(directoryPath + "\\filters").Select(x => System.IO.Path.GetFileNameWithoutExtension(x)).ToArray();
            FilterSelector.ItemsSource = filtersNames;
            ChangeTmp(initialImagePath);
        }

        private void ConfigureViewWindows()
        {
            InitialSample = new InitialSampleWindow();
            ResultSample = new InitialSampleWindow();
            InitialSample.Owner = this;
            ResultSample.Owner = this;
            InitialSample.Title = "Current view";
            ResultSample.Title = "Filtered view";           
        }

        private void Colorize(string patternForCommand, object[] args)
        {
            var res = ApplyCommand(patternForCommand, args);
            ChangeTmp(directoryPath + @"\tmp1.jpg");
        }


        private void ChangeTmp(string imgPath)
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(imgPath);
            image.EndInit();
            this.TmpIMG.Source = image;
            ResultSample.ViewImage.Source = image;
        }

        private object[] GetColorsDelta()
        {
            var redx = (int)(100 - this.redSlider.Value);
            var bluex = (int)(100 - this.blueSlider.Value);
            var greenx = (int)(100 - this.greenSlider.Value);

            var redy = (int)(this.redSlider.Value);
            var bluey = (int)(this.blueSlider.Value);
            var greeny = (int)(this.greenSlider.Value);
            return new object[] { initialImagePath, redx,redy, greenx, greeny, bluex, bluey, directoryPath+"\\tmp1.jpg" };
        }

        private string ApplyCommand(string argsFormat, object[] args)
        {
            var p = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    FileName = @"C:\ffmpeg\bin\ffmpeg.exe",
                    Arguments = string.Format(argsFormat, args),
                    CreateNoWindow = true
                }
            };
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return output;
        }

        private void Slider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            ApplyOptionsState();
        }

        private void RefreshSliders()
        {
            blueSlider.Value = 50;
            redSlider.Value = 50;
            greenSlider.Value = 50;
            SaturationBar.Value = 100;
            FilterSelector.SelectedIndex = -1;
            ApplyOptionsState();
        }

        private void CurvesShow_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(currentState);
            MessageBox.Show(currentState + " Copied to clipboard.");    
        }

        private void ExtendButtonInitialSample_Click(object sender, RoutedEventArgs e)
        {
            InitialSample.ViewImage.Source = this.StartIMG.Source;
            InitialSample.Show();
        }

        private void ExtendButtonResult_Click(object sender, RoutedEventArgs e)
        {
            ResultSample.ViewImage.Source = this.TmpIMG.Source;
            ResultSample.Show();
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog().Value)
            {
                ChangeTmp(dialog.FileName);
                initialImagePath = dialog.FileName;
                this.StartIMG.Source = new BitmapImage(new Uri(dialog.FileName));
                this.InitialSample.ViewImage.Source = this.StartIMG.Source;
                ChangeTmp(initialImagePath);
                RefreshSliders();
            }
        }

        private void FilterSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyOptionsState();
        }

        private void SaturationBar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            ApplyOptionsState();
        }

        private void ApplyOptionsState()
        {
            //Applying Colors
            Colorize(fullCurvesPattern, GetColorsDelta());

            //Applying Saturation
            var saturationValue = Math.Round(SaturationBar.Value / 100, 2);
            var pattern = @"-i ""{0}"" -filter_complex eq=saturation={1} ""{2}"" -y";
            var args = new object[] { directoryPath + "\\tmp1.jpg", saturationValue, directoryPath + "\\tmp1.jpg" };
            Colorize(pattern, args);

            //Applying Filter
            var filterPattern = @"-i ""{0}"" -vf curves=psfile=""{1}.acv"" ""{2}"" -y";
            //ffmpeg fix with escaping >_<
            var pathToFilter = (directoryPath + @"\filters\" + FilterSelector.SelectedItem).Replace("\\", "/").Replace(":", @"\\:");
            var command = string.Format(filterPattern,
                                            directoryPath + @"\tmp1.jpg",
                                            pathToFilter,
                                            directoryPath + @"\tmp1.jpg");
            var filterCommand = FilterSelector.SelectedIndex != -1 ? string.Format(@", curves=psfile={0}.acv", pathToFilter) : "" ;
            Colorize(command, new string[] { "empty" });

            //Create command chain
            currentState = CreateFinalChainCommand(string.Format(curvesModePattern, GetColorsDelta()), saturationValue, filterCommand);
        }

        private string CreateFinalChainCommand(string colorOptions, double saturationOption, string filterOption)
        {
            var finalChainCommand = string.Format(@"-vf ""{0}, eq=saturation={1}{2}""", colorOptions, saturationOption, filterOption);
            return finalChainCommand;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshSliders();
        }
    }
}
