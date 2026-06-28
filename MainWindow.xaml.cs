using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace are_you_there
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            float focusedVolume = (float)(FocusedSlider.Value / 100.0);
            float unfocusedVolume = (float)(UnfocusedSlider.Value / 100.0);
            AudioEngine.SetVolume(focusedVolume, unfocusedVolume);

            System.Threading.Tasks.Task.Run(() =>
            {
                AudioEngine.StartEngine();
            });
        }

        private void EndButton_Click(object sender, RoutedEventArgs e)
        {
            AudioEngine.EndEngine();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            float focusedVolume = (float)(FocusedSlider.Value / 100.0);
            float unfocusedVolume = (float)(UnfocusedSlider.Value / 100.0);
            AudioEngine.SetVolume(focusedVolume, unfocusedVolume);
            MessageBox.Show($"설정 적용됨: 집중 {FocusedSlider.Value}% / 비집중 {UnfocusedSlider.Value}%", "볼륨 설정", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}