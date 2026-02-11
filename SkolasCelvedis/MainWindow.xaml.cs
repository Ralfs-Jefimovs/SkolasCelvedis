using System;
using System.Collections.Generic;
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


using SharpVectors.Dom.Svg;
using SharpVectors.Renderers.Wpf;
using SharpVectors.Renderers.Utils;



namespace SkolasCelvedis
{
    public partial class MainWindow : Window
    {
        private bool isSecondFloor = false;

        private void ChangeButton_Click(object sender, RoutedEventArgs e)
        {
            isSecondFloor = !isSecondFloor;
            FloorSvgViewbox.Source = isSecondFloor ? new Uri("/Floors/second_floor.svg", UriKind.Relative) : new Uri("/Floors/first_floor.svg", UriKind.Relative);
        }
    }

    
}


// #TODO Pievienot drop down menu, lai izveletos stāvu, nevis tikai pogu, lai pārslēgtos starp diviem stāviem.
// Varētu būt arī iespēja izvēlēties konkrētu stāvu, ja ir vairāk nekā divi stāvi.

//#TODO Pievienot testēšanas iespēju, nospiežot uz jebkuras telpas, redzēt tās ID un nosaukumu, lai varētu vieglāk testēt un debugot.

//#TODO Pievienot pogu, kas ļauj lietotājam atgriezties uz sākuma punktu. Padomāt par iespējām pievienot animāciju, lai pāreja būtu gluda un vizuāli pievilcīga.