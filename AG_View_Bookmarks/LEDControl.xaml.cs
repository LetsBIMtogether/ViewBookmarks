// LEDControl.xaml.cs

#region Namespaces
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
#endregion

namespace CommonGuiWpf.Controls
{
    public enum LEDState
    {
        gray,
        green,
        red,
        yellow,
        // ... Etc ... As you want !

        unknown,
    }

    /// <summary>
    /// Interaction logic for LEDControl.xaml
    /// LEDControl is a WPF UserControl which permits to manage a LED with as many states as needed...
    ///  Each state is represented with a color
    /// </summary>
    public partial class LEDControl : UserControl, INotifyPropertyChanged
    {
        private const string IMAGES_LOCATION = "/AG_View_Bookmarks;component/Images/";
        private const string LED_IMAGES_PREFIX = "LED-";
        private const string IMAGES_EXTENSION = ".png";

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string name = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        #endregion

        public LEDControl()
        {
            InitializeComponent();
        }

        static private void LEDControl_PropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            LEDControl lc = d as LEDControl;
            if (lc != null)
                lc.RaisePropertyChanged("ImageResName");
        }

        public static readonly DependencyProperty StateProperty =
           DependencyProperty.Register("State", typeof(LEDState), typeof(LEDControl), new UIPropertyMetadata(LEDState.gray, LEDControl_PropertyChanged));

        public LEDState State
        {
            get { return (LEDState)GetValue(StateProperty); }
            set
            {
                SetValue(StateProperty, value);
                RaisePropertyChanged("ImageResName");
            }
        }

        public string ImageResName
        {
            get
            {
                return IMAGES_LOCATION + LED_IMAGES_PREFIX + State.ToString() + IMAGES_EXTENSION;
            }
        }
    }

    /// <summary>
    /// This converter can be used to bing a LEDControl on a nullable bool (bool?)
    ///   - When the nullable bool is set to null, LED will stay gray
    ///   - When the nullable bool is set to true, LED will become green
    ///   - When the nullable bool is set to false, LED will become red
    /// </summary>
    public class NullableBool2LEDStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
                System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return LEDState.gray;
            if (value is bool?)
            {
                if (((bool?)value).Value == true)
                    return LEDState.green;
                else
                    return LEDState.red;
            }
            return LEDState.unknown;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
                System.Globalization.CultureInfo culture)
        {
            if (value is LEDState)
            {
                if ((LEDState)value == LEDState.gray)
                    return null;
                else if ((LEDState)value == LEDState.green)
                    return true;
                else if ((LEDState)value == LEDState.red)
                    return false;
            }
            return null;
        }
    }

}
