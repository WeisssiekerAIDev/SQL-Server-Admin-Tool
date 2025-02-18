using System.Windows;

namespace SQLServerAdmin.Dialogs
{
    public partial class SaveTemplateDialog : Window
    {
        public string TemplateName => NameTextBox.Text;
        public string Description => DescriptionTextBox.Text;

        public SaveTemplateDialog()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TemplateName))
            {
                MessageBox.Show("Bitte geben Sie einen Template-Namen ein.", 
                    "Fehlender Name", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
