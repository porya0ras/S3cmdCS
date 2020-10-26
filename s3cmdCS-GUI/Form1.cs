using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using Telerik.WinControls.UI;

// This is the code for your desktop app.
// Press Ctrl+F5 (or go to Debug > Start Without Debugging) to run your app.

namespace s3cmdCS_GUI
{
    public partial class Form1 : Form
    {
        string accessKey, secretKey, endpoint;
        AmazonS3Client s3Client;
        AmazonS3Config config = new AmazonS3Config();
        private ListViewColumnSorter lvwColumnSorter;
        Thread upload_t;
        public Form1()
        {

            InitializeComponent();

            accessKey = getSetting("accessKey");
            secretKey = getSetting("secretKey");
            endpoint = getSetting("Endpoint");

            config.ServiceURL = endpoint;

            s3Client = new AmazonS3Client(
                    accessKey,
                    secretKey,
                    config
                    );

            listView1.Bounds = new Rectangle(new Point(10, 10), new Size(300, 200));
            // Set the view to show details.
            listView1.View = View.Details;
            // Allow the user to edit item text.
            listView1.LabelEdit = false;
            // Allow the user to rearrange columns.
            listView1.AllowColumnReorder = true;
            // Display check boxes.
            listView1.CheckBoxes = true;
            // Select the item and subitems when selection is made.
            listView1.FullRowSelect = true;
            // Display grid lines.
            listView1.GridLines = true;
            // Sort the items in the list in ascending order.
            listView1.Sorting = SortOrder.Ascending;

            // Create columns for the items and subitems.
            // Width of -2 indicates auto-size.
            lvwColumnSorter = new ListViewColumnSorter();
            this.listView1.ListViewItemSorter = lvwColumnSorter;
        }






        bool switch_connection = false;
        private void button1_Click(object sender, EventArgs e)
        {
            ListObjectsV2Request request = new ListObjectsV2Request();
            request.BucketName = BucketsList.SelectedValue.ToString();
            ListObjectsV2Response response = s3Client.ListObjectsV2(request);
            listView1.Clear();
            foreach (var row in response.S3Objects)
            {
                // Create three items and three sets of subitems for each item.
                ListViewItem item1 = new ListViewItem(row.Key.TrimEnd(), 0);
                item1.SubItems.Add(row.LastModified.ToString("G"));
                item1.SubItems.Add(row.Size.ToSize(MyExtension.SizeUnits.GB));
                item1.SubItems.Add(row.ETag);
                listView1.Items.Add(item1);
            }
            listView1.Columns.Add("File", 230, HorizontalAlignment.Left);
            listView1.Columns.Add("LastModified", 140, HorizontalAlignment.Left);
            listView1.Columns.Add("Size(GB)", -2, HorizontalAlignment.Left);
            listView1.Columns.Add("ETag", -2, HorizontalAlignment.Center);

        }

        private void listView1_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == lvwColumnSorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (lvwColumnSorter.Order == SortOrder.Ascending)
                {
                    lvwColumnSorter.Order = SortOrder.Descending;
                }
                else
                {
                    lvwColumnSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                lvwColumnSorter.SortColumn = e.Column;
                lvwColumnSorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            this.listView1.Sort();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            openFileDialog1 = new OpenFileDialog();
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var keyName = Path.GetFileName(openFileDialog1.FileName);

                    var fileTransferUtility = new TransferUtility(s3Client);

                    // Use TransferUtilityUploadRequest to configure options.
                    // In this example we subscribe to an event.
                    var uploadRequest =
                        new TransferUtilityUploadRequest
                        {
                            BucketName = BucketsList.SelectedValue.ToString(),
                            FilePath = openFileDialog1.FileName,
                            Key = keyName
                        };

                    uploadRequest.UploadProgressEvent += UploadRequest_UploadProgressEvent;

                    upload_t = new Thread(delegate ()
                    {
                        try
                        {
                            fileTransferUtility.Upload(uploadRequest);
                            MessageBox.Show("Upload completed!");
                            //progressBar1.Value = 0;
                        }
                        catch (AmazonS3Exception amazonS3Exception)
                        {
                            if (amazonS3Exception.ErrorCode != null &&
                                (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
                                ||
                                amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                            {
                                MessageBox.Show("Check the provided AWS Credentials.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            else
                            {
                                MessageBox.Show("Error occurred: " + amazonS3Exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    });
                    upload_t.Start();
                }
                catch (AmazonS3Exception amazonS3Exception)
                {
                    if (amazonS3Exception.ErrorCode != null &&
                        (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
                        ||
                        amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                    {
                        MessageBox.Show("Check the provided AWS Credentials.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        MessageBox.Show("Error occurred: " + amazonS3Exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

            }
        }

        private void UploadRequest_UploadProgressEvent(object sender, UploadProgressArgs e)
        {
            if (progressBar1.Parent.InvokeRequired)
            {
                progressBar1.Parent.Invoke(new MethodInvoker(delegate
                {
                    progressBar1.Maximum = 100;
                    progressBar1.Value = Convert.ToInt32(((double)e.TransferredBytes / e.TotalBytes) * 100);
                }));
            }
            else
            {
                progressBar1.Maximum = 100;
                progressBar1.Value = Convert.ToInt32(((double)e.TransferredBytes / e.TotalBytes) * 100);
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (upload_t != null)
                upload_t.Abort();
            Application.Exit();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are You Sure?", "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                if (MessageBox.Show("Are You Realy Sure?", "Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    foreach (ListViewItem item in this.listView1.CheckedItems)
                    {
                        var tag = item.Text;
                        // do some operation using tag
                        DeleteObjectRequest request = new DeleteObjectRequest();
                        request.BucketName = BucketsList.SelectedValue.ToString();
                        request.Key = tag;
                        var resp = s3Client.DeleteObject(request);
                        button1_Click(null, null);
                    }
                }
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            var fileTransferUtility = new TransferUtility(s3Client);
            saveFileDialog1.FileName = this.listView1.CheckedItems[0].Text;
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var downloadRequest =
                      new TransferUtilityDownloadRequest
                      {
                          BucketName = BucketsList.SelectedValue.ToString(),
                          FilePath = saveFileDialog1.FileName,
                          Key = this.listView1.CheckedItems[0].Text
                      };
                    downloadRequest.WriteObjectProgressEvent += DownloadRequest_WriteObjectProgressEvent;
                    upload_t = new Thread(delegate ()
                    {
                        try
                        {
                            fileTransferUtility.Download(downloadRequest);
                            MessageBox.Show("Download completed!");
                            //progressBar1.Value = 0;
                        }
                        catch (AmazonS3Exception amazonS3Exception)
                        {
                            if (amazonS3Exception.ErrorCode != null &&
                                (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
                                ||
                                amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                            {
                                MessageBox.Show("Check the provided AWS Credentials.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            else
                            {
                                MessageBox.Show("Error occurred: " + amazonS3Exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    });
                    upload_t.Start();
                }
                catch (AmazonS3Exception amazonS3Exception)
                {
                    if (amazonS3Exception.ErrorCode != null &&
                        (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
                        ||
                        amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                    {
                        MessageBox.Show("Check the provided AWS Credentials.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        MessageBox.Show("Error occurred: " + amazonS3Exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void DownloadRequest_WriteObjectProgressEvent(object sender, WriteObjectProgressArgs e)
        {
            if (progressBar1.Parent.InvokeRequired)
            {
                progressBar1.Parent.Invoke(new MethodInvoker(delegate
                {
                    progressBar1.Maximum = 100;
                    progressBar1.Value = Convert.ToInt32(((double)e.TransferredBytes / e.TotalBytes) * 100);
                }));
            }
            else
            {
                progressBar1.Maximum = 100;
                progressBar1.Value = Convert.ToInt32(((double)e.TransferredBytes / e.TotalBytes) * 100);
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ListBucketsResponse response = s3Client.ListBuckets();
            BucketsList.DataSource = response.Buckets;
            BucketsList.DisplayMember = "BucketName";
            BucketsList.ValueMember = "BucketName";

        }
        public string getSetting(string fild)
        {
            XDocument doc = XDocument.Load(Application.StartupPath + @"\Settings.xml");
            List<XElement> setting = doc.Root.Elements().Where(x => x.Name.LocalName == fild).ToList<XElement>();
            return setting.First().Value;
        }
    }
}
