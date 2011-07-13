using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using hOOt;
using System.Threading;
using System.Diagnostics;


namespace SampleApp
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        Hoot h;

        private void button2_Click(object sender, EventArgs e)
        {
            MessageBox.Show("" + h.WordCount());
        }

        private void button3_Click(object sender, EventArgs e)
        {
            h.FreeMemory(false);
            GC.Collect(2);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            h.Save();
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            if (h == null)
            {
                MessageBox.Show("hOOt not loaded");
                return;
            }
            listBox1.Items.Clear();
            DateTime dt = DateTime.Now;
            foreach (Document d in h.FindDocuments(txtSearch.Text))
            {
                listBox1.Items.Add(d);
            }
            lblStatus.Text = "Search = " + listBox1.Items.Count + " items, " + DateTime.Now.Subtract(dt).TotalSeconds + " s";
        }

        private void button7_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog fbd = new FolderBrowserDialog();
            fbd.SelectedPath = Directory.GetCurrentDirectory();
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
            if (h == null)
                h = new Hoot(Path.GetFullPath(txtIndexFolder.Text), "index");

            string[] files = Directory.GetFiles(txtWhere.Text, "*", SearchOption.AllDirectories);
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
                    TextReader tf = new EPocalipse.IFilter.FilterReader(fn);
                    string s = "";
                    if (tf != null)
                        s = tf.ReadToEnd();

                    h.Index(new Document(fn, s), true);
                }
                catch { }
                i++;
                if (i > 3000)
                {
                    i = 0;
                    h.Save();
                }
            }
            h.Save();
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            lblIndexer.Text = "" + e.UserState;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnStart.Enabled = true;
            btnStop.Enabled = false;
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
            if (txtIndexFolder.Text == "")
            {
                MessageBox.Show("Please supply the index storage folder.");
                return;
            }

            h = new Hoot(Path.GetFullPath(txtIndexFolder.Text), "index");
            button1.Enabled = false;
        }
    }
}
