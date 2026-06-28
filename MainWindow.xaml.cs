using System.ComponentModel;
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

            // UI 상태 업데이트 (초록색 텍스트)
            StatusText.Text = "상태: 작동 중 🟢";
            StatusText.Foreground = new SolidColorBrush(Colors.Green);
        }

        private void EndButton_Click(object sender, RoutedEventArgs e)
        {
            AudioEngine.EndEngine();

            // UI 상태 업데이트 (기본색 텍스트)
            StatusText.Text = "상태: 대기 중 ⚪";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85));
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            float focusedVolume = (float)(FocusedSlider.Value / 100.0);
            float unfocusedVolume = (float)(UnfocusedSlider.Value / 100.0);
            AudioEngine.SetVolume(focusedVolume, unfocusedVolume);

            MessageBox.Show($"설정 적용됨: 집중 {FocusedSlider.Value}% / 비집중 {UnfocusedSlider.Value}%", "볼륨 설정", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 창의 X 버튼을 눌러서 끌 때 자동으로 엔진을 끄고 볼륨을 복구하는 안전 장치
        protected override void OnClosing(CancelEventArgs e)
        {
            AudioEngine.EndEngine();
            base.OnClosing(e);
        }
    }
}