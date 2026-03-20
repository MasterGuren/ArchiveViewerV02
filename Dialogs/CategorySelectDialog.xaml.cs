using System.Windows;
using System.Windows.Input;

namespace ArchiveViewer.Dialogs;

public partial class CategorySelectDialog : Window
{
    public string SelectedCategory => CategoryList.SelectedItem is string s && s != "(なし)" ? s : "";

    public CategorySelectDialog(List<string> categories, string currentCategory)
    {
        InitializeComponent();

        CategoryList.Items.Add("(なし)");
        foreach (var cat in categories)
            CategoryList.Items.Add(cat);

        if (string.IsNullOrEmpty(currentCategory))
            CategoryList.SelectedIndex = 0;
        else
        {
            var idx = categories.IndexOf(currentCategory);
            if (idx >= 0) CategoryList.SelectedIndex = idx + 1; // +1 for "(なし)"
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void CategoryList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CategoryList.SelectedIndex >= 0)
            DialogResult = true;
    }
}
