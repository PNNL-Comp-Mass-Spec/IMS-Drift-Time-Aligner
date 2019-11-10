using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;
using NDtw;

namespace IMSDriftTimeAligner
{
    /// <summary>
    /// Interaction logic for DTWVisualization.xaml
    /// </summary>
    public partial class DTWVisualization : Window, INotifyPropertyChanged
    {
        private IDtw _dtw;

        /// <summary>
        /// DTW instance
        /// </summary>
        public IDtw Dtw
        {
            get => _dtw;
            set
            {
                _dtw = value;
                OnPropertyChanged(nameof(Dtw));
            }
        }

        private string _description;

        /// <summary>
        /// Description of the data
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(Description));
            }
        }

        //private bool _drawDistance;
        //public bool DrawDistance
        //{
        //    get => _drawDistance;
        //    set
        //    {
        //        _drawDistance = value;
        //        if (value && DrawCost)
        //            DrawCost = false;

        //    }
        //}

        //private bool _drawCost;

        //public bool DrawCost
        //{
        //    get => _drawCost;
        //    set
        //    {
        //        _drawCost = value;
        //        if (value && DrawDistance)
        //            DrawDistance = false;
        //    }
        //}

        /// <summary>
        /// Constructor
        /// </summary>
        public DTWVisualization()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Event fired when a property is updated
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
