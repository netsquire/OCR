using System;
using System.Collections.Generic;
using System.Diagnostics;
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

using Microsoft.Win32;
using Tesseract;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;

namespace OCR4.MainWindow {
    public partial class ListViewOCR : Window     {
        ObservableCollection<OCRDocument> docs = new ObservableCollection<OCRDocument>();

        public ListViewOCR()  {
            InitializeComponent();           
            lvUsers.ItemsSource = this.docs;
        }

        private void Load_Document(object sender, RoutedEventArgs e) {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            //openFileDialog.InitialDirectory = @"ocr_images/";
            if (openFileDialog.ShowDialog() == true) {
                string chosenFile = openFileDialog.FileName;
                selectedFiles.Text = chosenFile;                
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(chosenFile);
                bitmap.EndInit();
                DocumentImage.Source = bitmap;
            }
        }

        private void Process(object sender, RoutedEventArgs e) {
            string ocrFile = selectedFiles.Text;
            OCRDocument doc;
            if (ocrFile == "")
                MessageBox.Show("We don't process empty files!");
            else {
                if (ocrFile.StartsWith("ocr_images")) {
                    ocrFile = @"C:\Users\IEUser\documents\visual studio 2015\Projects\OCR4\OCR4\ocr_images\DefaultDocument.png";
                }                
                //const string tessDataDir = @"C:\Program Files\Tesseract-OCR\tessdata";
                const string tessDataDir = "tessdata";
                using (var engine = new TesseractEngine(tessDataDir, "eng", EngineMode.Default))
                using (var image = Pix.LoadFromFile(ocrFile))
                using (var page = engine.Process(image))    {
                    string text = page.GetText();
                    //MessageBox.Show(text);
                    //System.IO.File.WriteAllText(ocrFile + ".txt", text);
                    doc = new OCRecognizer().textParsingAssignment(text); // TODO: to make it static or fabric ?
                }
                this.docs.Add(doc);
            }
        }        
    }

    public class OCRecognizer {
        public OCRDocument textParsingAssignment(string text) {
            OCRDocument doc = new OCRDocument() { Name = "", Created = "", From = "", Topic = "", Type = "" };
            Regex parts = new Regex(@"^\d+\t(\d+)\t.+?\t(item\\[^\t]+\.ddj)");
            Regex subjectRegex = new Regex(@"Subject:\s*(.+)$");
            Regex emailRegex = new Regex(@"Email:\s*(.+)$");
            Regex toRegex = new Regex(@"To:\s*(.+)$");
            Regex preToRegex = new Regex(@"Ms.\s+(.+)");
            Regex dateRegex = new Regex(@"Date\s*(.+)");

            string months = "(January|February|March|April|May|June|July|August|September|October|November|December)";
            Regex createdRegex1 = new Regex(@"(.+)\s+" + months + @"\s+(\d+)");
            Regex createdRegex2 = new Regex(months + @"\s(\d+),\s+(\d+)");
            Regex createdRegex3 = new Regex(@"Date\s.+(\d+)/(\d+)/(\d+)");

            string topic = "", from = "", to = "", date = "", created = "", type = "", name = "", preTo="";
            string[] lines = text.Split("\r\n".ToCharArray());
            Match match;

            // === I phase
            foreach (string line in lines)  {
                if (topic == "")   {
                    topic = catchRegEx(line, subjectRegex, 1);
                    doc.Topic = topic;
                    }
                if (from == "")    {
                    from = catchRegEx(line, emailRegex, 1);
                    doc.From = from;
                }
                if (to == "")      {
                    to = catchRegEx(line, toRegex, 1);
                    doc.To = to;
                }
                if (preTo == "")
                {
                    preTo = catchRegEx(line, preToRegex, 1);
                }


                if (created == "")   {
                    match = createdRegex1.Match(line);
                    if (match.Success) {
                        string day = match.Groups[1].Value;
                        string month = match.Groups[2].Value;
                        string year = match.Groups[3].Value;
                        created = day + " " + month + " " + year;
                        doc.Created = created;
                    }                   
                }
                if (created == "")  {
                    match = createdRegex2.Match(line);
                    if (match.Success) {
                        string month = match.Groups[1].Value;
                        string day = match.Groups[2].Value;                        
                        string year = match.Groups[3].Value;
                        created = month + " " + day + ", " + year;
                        doc.Created = created;
                    }
                }
                if (created == "") {
                    match = createdRegex3.Match(line);
                    if (match.Success)  {                        
                        string day = match.Groups[1].Value;
                        string month = match.Groups[2].Value;
                        string year = match.Groups[3].Value;
                        created = " " + day + "/" + month + "/" + year;
                        doc.Created = created;
                    }
                }


                if (date == "")    {
                    date = catchDate(line);
                    doc.Date = date;
                }
            }

            // === II phase
            string[] clearText = clearArray(lines);
            int len = clearText.Length;

            if (topic == "") {
                topic = clearText[0];
                doc.Topic = topic;
            }

            if (name == "") {
                name = clearText[len - 2];
                doc.Name = name;
            }
            
            if (to == "") {
                toRegex = new Regex(@"To:\s*[\r\n]*(.+)");
                match = toRegex.Match(text);
                if (match.Success)  {
                    to = match.Groups[1].Value;
                    doc.To = to;
                }
            }
            if (to == "")  {
                to = preTo;
                doc.To = to;
                }

            return doc;
        }

        private string[] clearArray(string[] lines) {
            List<string> ret = new List<string>();
            foreach (string line in lines) {
                if (line != "" && line != " ") { ret.Add(line); }
            }
            return ret.ToArray();
        }
        
        private string catchDate(string line)   {
            DateTime dateValue;
            string ret = "";
            string content = new Regex(@"(\w+)").Match(line).Groups[1].Value;
            if (DateTime.TryParse(content, out dateValue)) {
                ret = content;
            }
            return ret;
        }
        
        private string catchRegEx(string line, Regex pattern, int group) {
            string ret = "";
            Match match = pattern.Match(line);
            Group rest = match.Groups[group];
            if (match.Success)   {
                ret = rest.Value;
            }
            return ret;
        }

    }

    public class OCRDocument    {
        public string Name { get; set; }
        public string Created { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Topic { get; set; }
        public string Date { get; set; }
        public string Type { get; set; }        
    }    
}
