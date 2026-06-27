using are_you_ther;
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
            // UI 스레드가 얼지 않도록 별도 스레드에서 엔진 실행
            System.Threading.Tasks.Task.Run(() =>
            {
                AudioEngine.SetVolume(1.0f, 0.1f);
                AudioEngine.StartEngine();
            });
        }
        private void EndButton_Click(object sender, RoutedEventArgs e)
        {
            AudioEngine.EndEngine();
        }
    }
}