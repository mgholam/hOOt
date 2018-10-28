using System;
using System.ComponentModel;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using RaptorDB;

namespace SampleApp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        Hoot hoot;
        DateTime _indextime;

        private void button2_Click(object sender, EventArgs e)
        {
            if (hoot == null)
                loadhoot();
            MessageBox.Show("Words = " + hoot.WordCount.ToString("#,#") + "\r\nDocuments = " + hoot.DocumentCount.ToString("#,#"));
        }

        private void button4_Click(object sender, EventArgs e)
        {
            hoot.Save();
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            if (hoot == null)
                loadhoot();

            listBox1.Items.Clear();
            DateTime dt = DateTime.Now;
            listBox1.BeginUpdate();

            foreach (var d in hoot.FindDocumentFileNames(txtSearch.Text))
            {
                listBox1.Items.Add(d);
            }
            listBox1.EndUpdate();
            lblStatus.Text = "Search = " + listBox1.Items.Count + " items, " + DateTime.Now.Subtract(dt).TotalSeconds + " s";
        }

        private void button7_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = txtWhere.Text;
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                txtWhere.Text = fbd.SelectedPath;
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (txtIndexFolder.Text == "" || txtWhere.Text == "")
            {
                MessageBox.Show("Please supply the index storage folder and the where to start indexing from.");
                return;
            }

            btnStart.Enabled = false;
            btnStop.Enabled = true;
            loadhoot();

            string[] files = Directory.GetFiles(txtWhere.Text, "*", SearchOption.AllDirectories);
            _indextime = DateTime.Now;
            backgroundWorker1.RunWorkerAsync(files);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = Directory.GetCurrentDirectory();
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                txtIndexFolder.Text = fbd.SelectedPath;
            }
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            string[] files = e.Argument as string[];
            BackgroundWorker wrk = sender as BackgroundWorker;
            int i = 0;
            foreach (string fn in files)
            {
                if (wrk.CancellationPending)
                {
                    e.Cancel = true;
                    break;
                }
                backgroundWorker1.ReportProgress(1, fn);
                try
                {
                    if (hoot.IsIndexed(fn) == false)
                    {
                        using (TextReader tf = new EPocalipse.IFilter.FilterReader(fn))
                        {
                            string s = "";
                            if (tf != null)
                                s = tf.ReadToEnd();

                            hoot.Index(new myDoc(new FileInfo(fn), s), true);
                        }
                    }
                }
                catch { }
                i++;
                if (i > 1000)
                {
                    i = 0;
                    hoot.Save();
                }
            }
            hoot.Save();
            hoot.OptimizeIndex();
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lblIndexer.Text = "" + e.UserState;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnStart.Enabled = true;
            btnStop.Enabled = false;
            lblIndexer.Text = "" + DateTime.Now.Subtract(_indextime).TotalSeconds + " sec";
            MessageBox.Show("Indexing done : " + DateTime.Now.Subtract(_indextime).TotalSeconds + " sec");
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            backgroundWorker1.CancelAsync();
        }

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                btnSearch_Click(null, null);
        }

        private void listBox1_DoubleClick(object sender, EventArgs e)
        {
            Process.Start("" + listBox1.SelectedItem);
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            loadhoot();
        }

        private void loadhoot()
        {
            if (txtIndexFolder.Text == "")
            {
                MessageBox.Show("Please supply the index storage folder.");
                return;
            }

            hoot = new Hoot(Path.GetFullPath(txtIndexFolder.Text), "index", true);
            button1.Enabled = false;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (hoot != null)
                hoot.Shutdown();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            // free memory
            hoot.FreeMemory();
            GC.Collect(GC.MaxGeneration);
        }
    }
}
