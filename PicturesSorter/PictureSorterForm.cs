﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PicturesSorter
{
    public partial class PictureSorterForm : Form
    {
        private class Nodes : Tuple<LinkedListNode<ImageHost>, LinkedListNode<ImageHost>> {
            public Nodes(LinkedListNode<ImageHost> item1, LinkedListNode<ImageHost> item2) : base(item1, item2) { }
        }

        DirectoryInfo _currentDirectory = null;
        LinkedList<ImageHost> _currentFiles = null;
        Nodes fileIndex;

        public PictureSorterForm()
        {
            InitializeComponent();
        }

        private void PictureSorterForm_Resize(object sender, EventArgs e)
        {
            pictureBox1.Width = pictureBox2.Width = ClientRectangle.Width / 2;
            label1.Width = label2.Width = ClientRectangle.Width / 2;
        }

        private void PictureSorterForm_Load(object sender, EventArgs e)
        {
            OpenFolder();
        }
        private void OpenFolder() {
            folderBrowserDialog.ShowDialog();
            Text = folderBrowserDialog.SelectedPath;
            _currentDirectory = new DirectoryInfo(folderBrowserDialog.SelectedPath);
            _currentFiles = new LinkedList<ImageHost>(
                    _currentDirectory
                    .EnumerateFiles("*.jpg")
                    .Select(f => f as FileInfo)
                    .Where(f => f != null)
                    .OrderBy(f => f.Name)
                    .Select(f => new ImageHost { FileInfo = f })
                );
            switch (_currentFiles.Count)
            {
                case 0:
                    MessageBox.Show($"There are no pictures to sort in folder {folderBrowserDialog.SelectedPath}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    fileIndex = new Nodes(null, null);
                    return;
                case 1:
                    fileIndex = LoadPictures(new Nodes(_currentFiles.First, _currentFiles.First), 0, 0, noRelease:true);
                    break;
                default:
                    fileIndex = LoadPictures(new Nodes(_currentFiles.First, _currentFiles.First.Next), 0, 0, noRelease: true);
                    break;
            }
        }
        private Nodes LoadPictures(Nodes idx, int step1, int step2, bool noRelease = false)
        {
            Nodes rc = SelectIndexes(idx, step1, step2);
            rc.Item1.Value.Render(pictureBox1, label1);
            rc.Item2.Value.Render(pictureBox2, label2);
            if (!noRelease)
            {
                idx?.Item1.Value.Release();
                idx?.Item2.Value.Release();
            }
            return rc;
        }

        private Nodes SelectIndexes(Nodes idx, int step1, int step2)
            => new Nodes(idx.Item1.SafeStep(step1), idx.Item2.SafeStep(step2));

        private void LoadPicture(PictureBox pb, Label lbl, FileSystemInfo fi)
        {
            lbl.Text = fi.FullName;
            pb.SizeMode = PictureBoxSizeMode.Zoom;
            using (var stream = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read))
            {
                pb.Image = Image.FromStream(stream);
            }
            pb.Refresh();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            return ProcessCmdKeyImpl(ref msg, keyData);
        }

        protected bool ProcessCmdKeyImpl(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Left : fileIndex = LoadPictures(fileIndex, -1, -1); break;
                case Keys.Right: fileIndex = LoadPictures(fileIndex, +1, +1); break;
                case Keys.Control | Keys.Left : fileIndex = LoadPictures(fileIndex, -1, 0); break;
                case Keys.Control | Keys.Right: fileIndex = LoadPictures(fileIndex, 0, +1); break;
                case Keys.NumPad1:
                case Keys.D1:
                    {
                        var tbd = fileIndex.Item1.Value;
                        fileIndex = LoadPictures(fileIndex, -1, 0);
                        tbd.ArchivePicture();
                        tbd.Dispose();
                        _currentFiles.Remove(tbd);
                    }
                    break;
                case Keys.NumPad2:
                case Keys.D2:
                    {
                        var tbd = fileIndex.Item2.Value;
                        fileIndex = LoadPictures(fileIndex, 0, 1);
                        tbd.ArchivePicture();
                        tbd.Dispose();
                        _currentFiles.Remove(tbd);
                    }
                    break;
                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
            return true;
        }

        private void previousToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Message msg=new Message();
            ProcessCmdKeyImpl(ref msg, Keys.Left);
        }

        private void nextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Message msg = new Message();
            ProcessCmdKeyImpl(ref msg, Keys.Right);
        }

        private void leftPreviousToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Message msg = new Message();
            ProcessCmdKeyImpl(ref msg, Keys.Left);
        }

        private void rightNextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Message msg = new Message();
            ProcessCmdKeyImpl(ref msg, Keys.Right);
        }

        private void archiveLeftToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Message msg = new Message();
            ProcessCmdKeyImpl(ref msg, Keys.D1);
        }

        private void archiveRightToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Message msg = new Message();
            ProcessCmdKeyImpl(ref msg, Keys.D2);
        }

        private void pickDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFolder();
        }
    }

    static class ArrayExtensions {
        public static int NextNonNullIndex<T>(this T[] a, int startIndex, int step) 
        {
            for( var i = 1; i <= a.Length;i++)
                if(!Equals(a[(startIndex + i * step + a.Length) % a.Length], default(T)))
                    return (startIndex + i * step + a.Length) % a.Length;
            return -1;
        }
    }

    static class GenericExtentions {
        public static LinkedListNode<T> SafeNext<T>(this LinkedListNode<T> lln) => lln.Next ?? lln.List.First;
        public static LinkedListNode<T> SafePrev<T>(this LinkedListNode<T> lln) => lln.Previous ?? lln.List.Last;
        public static LinkedListNode<T> SafeStep<T>(this LinkedListNode<T> lln, int step) 
            => step < 0 ? lln.SafePrev()
             : step > 0 ? lln.SafeNext()
             : lln;
    }

    class ImageHost : IDisposable {
        private Image _image;
        public  Image Image
        {
            get
            {
                if (_image == null)
                {
                    using (var stream = new FileStream(FileInfo.FullName, FileMode.Open, FileAccess.Read))
                    {
                        _image = Image.FromStream(stream);
                    }
                }
                return _image;
            }
        }
        public FileInfo FileInfo { get; set; }
        public string FullName => FileInfo.FullName;

        private int _useCount;
        public void Release()
        {
            if (--_useCount >= 0)
            {
                Dispose();
                _useCount = 0;
            }
        }

        public void ArchivePicture()
        {
            if (FileInfo == null) return;
            if (FileInfo.Exists)
            {
                var di = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(FileInfo.FullName), "spare"));
                di.Create();
                File.Move(FileInfo.FullName, Path.Combine(di.FullName, FileInfo.Name));
            }
        }

        public void Dispose()
        {
            _image?.Dispose();
            _image = null;
        }

        public void Render(PictureBox pictureBox, Label label)
        {
            if (label.Text != FileInfo.Name)
            {
                pictureBox.Image = Image;
                label.Text = FileInfo.Name;
                pictureBox.Refresh();
            }
            _useCount++;
        }
    }
}
