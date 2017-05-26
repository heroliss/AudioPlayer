using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CustomRandom;
namespace AudioPlayer
{
    public partial class Form1 : Form
    {
        public string xmlFileName = "TimeData.xml";
        public XMLManager xmlManager;
        public string timeFormat = @"hh\:mm\:ss";
        public string percentFormat = "0.00%";

        public NoRepeatRandom random;
        public List<ListViewItem> playRecord;
        public int playRecordIndex = -1;
        public int playRecordEndIndex = -1;
        public int loopStyle = 0; //0：不续播，1：顺序，2：表单循环，3：随机，4：单曲循环
        public int skipErrorFileCount = 0;
        public int skipErrorFileCountLimit = 10;

        public Color PlayingItemBackColor = Color.LightGreen;
        public Color PauseItemBackColor = Color.Yellow;
        public Color StopItemBackColor = Color.LightGray;
        public Color DefaultItemBackColor = Color.White;
        public Color ErrorItemBackColor = Color.PaleVioletRed;
        public Color ErrorMarkItemBackColor = Color.Pink;

        public bool? playError = null;
        public int currentPlayIndex = -1;

        public int PlayProcess //播放进度
        {
            get
            {
                long positionSmallChange = baStream.Length / trackBar1.Maximum;
                return (int)(baStream.Position / positionSmallChange);
            }
            set
            {
                long positionSmallChange = baStream.Length / trackBar1.Maximum;
                long position = value * positionSmallChange;
                position = position / baStream.BlockAlign * baStream.BlockAlign; //块对齐
                baStream.Position = position;
            }
        }

        Mp3FileReader rdr;
        WaveStream wavStream;
        BlockAlignReductionStream baStream;
        WaveOut waveOutDevice;

        public Form1()
        {
            InitializeComponent();
            playRecord = new List<ListViewItem>();
            waveOutDevice = new WaveOut(WaveCallbackInfo.FunctionCallback());
            waveOutDevice.PlaybackStopped += WaveOutDevice_PlaybackStopped;
            xmlManager = new XMLManager(xmlFileName);
        }
        private void resetRandom()
        {
            random = new NoRepeatRandom(0, listView1.Items.Count);
        }
        private void WaveOutDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (baStream.Position == baStream.Length)
            {
                resetItemBackColor();
                自动续播();
                updateUI();
            }
        }

        private void 自动续播()
        {
            switch (loopStyle)
            {
                case 0:
                    break;
                case 1: //顺序
                    if (currentPlayIndex == listView1.Items.Count - 1)
                    {
                        break;
                    }
                    playNext();
                    break;
                case 2: //表单循环
                    playNext();
                    break;
                case 3: //随机播放
                    PlayAudioByRandom();
                    break;
                case 4: //单曲循环
                    PlayAudioByIndex(currentPlayIndex);
                    break;
                default:
                    break;
            }
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            if (waveOutDevice.PlaybackState != PlaybackState.Stopped)
            {
                PlayProcess = trackBar1.Value;
            }
        }
        private void PlayAudioByIndex(int audioIndex, bool recordOn = true)
        {
            if (waveOutDevice.PlaybackState != PlaybackState.Stopped)
            {
                waveOutDevice.Stop();
            }
            if (recordOn)
            {
                playRecordIndex++;
                playRecordEndIndex = playRecordIndex;
                playRecord.Insert(playRecordIndex, listView1.Items[audioIndex]);
            }
            currentPlayIndex = audioIndex;

            FileInfo fileInfo = listView1.Items[audioIndex].Tag as FileInfo;
            long? position = listView1.Items[audioIndex].SubItems[1].Tag as long?;
            playError = !PlayAudio(fileInfo.FullName,position == null ? 0 : position.Value);
            if (playError == true)
            {
                updateUI();
                Update();
                if (loopStyle != 0 && loopStyle != 4 && recordOn == true)
                {
                    skipErrorFileCount++;
                    if (skipErrorFileCount > skipErrorFileCountLimit)
                    {
                        skipErrorFileCount = 0;
                        return;
                    }
                    playNext();
                }

            }
            skipErrorFileCount = 0;
        }

        private bool PlayAudio(String path,long position)
        {
            try
            {
                rdr = new Mp3FileReader(path);
                wavStream = WaveFormatConversionStream.CreatePcmStream(rdr);
                baStream = new BlockAlignReductionStream(wavStream);
                waveOutDevice.Init(baStream);
                position = position / baStream.BlockAlign * baStream.BlockAlign;
                baStream.Position = position;
                waveOutDevice.Play();
            }
            catch
            {
                return false;
            }
            return true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            trackBar1.Value = PlayProcess;
            listView1.Items[currentPlayIndex].SubItems[1].Tag = baStream.Position;
            listView1.Items[currentPlayIndex].SubItems[1].Text = baStream.CurrentTime.ToString(timeFormat);
            listView1.Items[currentPlayIndex].SubItems[3].Text = ((float)baStream.Position / baStream.Length).ToString(percentFormat);

        }

        private void listView1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.All;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void listView1_DragDrop(object sender, DragEventArgs e)
        {
            string[] s = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            for (int i = 0; i < s.Length; i++)
            {
                FileInfo fileInfo = new FileInfo(s[i]);
                ListViewItem item = listView1.Items.Add(fileInfo.Name);
                item.Tag = fileInfo;
                item.SubItems.Add("-");
                item.SubItems.Add("-");
                item.SubItems.Add("-");
                item.SubItems.Add("-");
                try
                {
                    Mp3FileReader reader = new Mp3FileReader(fileInfo.FullName);
                    item.SubItems[2].Text = reader.TotalTime.ToString(timeFormat);
                    item.SubItems[3].Text = string.Format(percentFormat, 0);
                    item.SubItems[4].Text = reader.WaveFormat.ToString();
                    reader.Close();
                }
                catch
                {
                    item.BackColor = ErrorMarkItemBackColor;
                }
            }
            resetRandom();
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            playFocusedItem();
        }

        private void playFocusedItem()
        {
            resetItemBackColor();
            PlayAudioByIndex(listView1.FocusedItem.Index);
            updateUI();
            listView1.FocusedItem.Selected = false;
        }

        private void updateUI()
        {
            if (currentPlayIndex < 0)
            {
                return;
            }
            switch (waveOutDevice.PlaybackState)
            {
                case PlaybackState.Playing:
                    timer1.Start();
                    buttonPlayPause.Text = "||";
                    listView1.Items[currentPlayIndex].BackColor = PlayingItemBackColor;
                    break;
                case PlaybackState.Paused:
                    timer1.Stop();
                    buttonPlayPause.Text = ">";
                    listView1.Items[currentPlayIndex].BackColor = PauseItemBackColor;
                    break;
                case PlaybackState.Stopped:
                    timer1.Stop();
                    buttonPlayPause.Text = ">";
                    listView1.Items[currentPlayIndex].BackColor
                        = playError == true ? ErrorItemBackColor : StopItemBackColor;
                    trackBar1.Value = 0;
                    break;
                default:
                    break;
            }
        }

        private void buttonPlayPause_Click(object sender, EventArgs e)
        {
            switch (waveOutDevice.PlaybackState)
            {
                case PlaybackState.Stopped:
                    if (listView1.FocusedItem != null)
                    {
                        resetItemBackColor();
                        PlayAudioByIndex(listView1.FocusedItem.Index);
                        listView1.FocusedItem.Focused = false;
                    }
                    else if (listView1.Items.Count != 0)
                    {
                        resetItemBackColor();
                        if (currentPlayIndex == -1)
                        {
                            currentPlayIndex = 0;
                        }
                        PlayAudioByIndex(currentPlayIndex);
                    }
                    break;
                case PlaybackState.Playing:
                    waveOutDevice.Pause();
                    break;
                case PlaybackState.Paused:
                    waveOutDevice.Resume();
                    break;
            }
            updateUI();
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            stopPlay();
        }

        private void stopPlay()
        {
            if (waveOutDevice.PlaybackState != PlaybackState.Stopped)
            {
                waveOutDevice.Stop();
                listView1.Items[currentPlayIndex].SubItems[1].Tag = 0;
                listView1.Items[currentPlayIndex].SubItems[1].Text = "-";
                listView1.Items[currentPlayIndex].SubItems[3].Text = "-";
                updateUI();
            }
        }

        private void buttonNext_Click(object sender, EventArgs e)
        {
            playNext();
        }

        private void playNext()
        {
            if (currentPlayIndex == -1)
            {
                return;
            }
            int nextIndex = currentPlayIndex + 1;
            resetItemBackColor();
            switch (loopStyle)
            {
                case 0:
                case 1: //顺序
                case 4: //单曲循环
                    if (nextIndex < listView1.Items.Count)
                    {
                        PlayAudioByIndex(nextIndex);
                    }
                    break;
                case 2: //表单循环
                    if (nextIndex >= listView1.Items.Count)
                    {
                        nextIndex = 0;
                    }
                    PlayAudioByIndex(nextIndex);
                    break;
                case 3: //随机播放
                    PlayAudioByRandom();
                    break;

                default:
                    break;
            }
            updateUI();
        }

        private void PlayAudioByRandom()
        {
            resetItemBackColor();
            try
            {
                PlayAudioByIndex(random.Next());

            }
            catch (ApplicationException)
            {
                random.reset();
                PlayAudioByRandom();
            }
        }

        private void buttonLast_Click(object sender, EventArgs e)
        {
            playLast();
        }

        private void playLast()
        {
            if (currentPlayIndex == -1)
            {
                return;
            }
            resetItemBackColor();

            int lastIndex = currentPlayIndex - 1;
            if (lastIndex < 0)
            {
                lastIndex = listView1.Items.Count - 1;
            }

            PlayAudioByIndex(lastIndex);
            updateUI();
        }

        private void resetItemBackColor()
        {
            if (currentPlayIndex < 0)
            {
                return;
            }
            if (playError == true) //文件错误
            {
                listView1.Items[currentPlayIndex].BackColor = ErrorMarkItemBackColor;
                return;
            }
            else if (playError == false) //正常播放
            {
                listView1.Items[currentPlayIndex].BackColor = DefaultItemBackColor;
            }
            //playError == null 表示未知
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            loopStyle = 0;
            waveOutDevice.Stop();
            foreach (ListViewItem item in listView1.Items)
            {
                FileInfo fileInfo = item.Tag as FileInfo;
                long? position = item.SubItems[1].Tag as long?;
                xmlManager.addItem(fileInfo.FullName, position == null ? -1 : position.Value);
            }
            try
            {
                xmlManager.save();
            }
            catch
            {
                MessageBox.Show("XML文件保存失败！");
            }
        }

        private void 切换选中状态并设置循环方式(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            foreach (ToolStripMenuItem i in 自动续播ToolStripMenuItem.DropDownItems)
            {
                i.Checked = false;
            }
            item.Checked = true;
            loopStyle = int.Parse(item.Tag as string);
        }

        private void 从列表中删除ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                if (item.Index < currentPlayIndex)
                {
                    currentPlayIndex--;
                }
                else if (item.Index == currentPlayIndex)
                {
                    stopPlay();
                    currentPlayIndex--;
                }
                item.Remove();
                playError = null;
            }
            resetRandom();
        }

        private void buttonBack_Click(object sender, EventArgs e)
        {
            back();
        }

        private void back()
        {
            if (playRecordIndex <= 0)
            {
                return;
            }
            int i = playRecord[--playRecordIndex].Index;
            if (i == -1) //该项已被删除
            {
                playRecord[playRecordIndex].Remove();
                back();
                return;
            }
            resetItemBackColor();
            PlayAudioByIndex(i, false);
            updateUI();
        }

        private void buttonForward_Click(object sender, EventArgs e)
        {
            forward();
        }

        private void forward()
        {
            if (playRecordIndex >= playRecordEndIndex)
            {
                return;
            }
            int i = playRecord[++playRecordIndex].Index;
            if (i == -1) //该项已被删除
            {
                playRecord[playRecordIndex].Remove();
                forward();
                return;
            }
            resetItemBackColor();
            PlayAudioByIndex(i, false);
            updateUI();
        }

        private void 播放ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            playFocusedItem();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            foreach (var item in xmlManager.timeDataList)
            {
                FileInfo fileInfo = new FileInfo(item.Key);
                ListViewItem liv = listView1.Items.Add(fileInfo.Name);
                liv.Tag = fileInfo;
                liv.SubItems.Add("-");
                liv.SubItems.Add("-");
                liv.SubItems.Add("-");
                liv.SubItems.Add("-");
                try
                {
                    Mp3FileReader reader = new Mp3FileReader(fileInfo.FullName);
                    reader.Position = item.Value;
                    liv.SubItems[1].Tag = item.Value;
                    liv.SubItems[1].Text = reader.CurrentTime.ToString(timeFormat);
                    liv.SubItems[2].Text = reader.TotalTime.ToString(timeFormat);
                    liv.SubItems[3].Text = ((float)reader.Position / reader.Length).ToString(percentFormat);
                    liv.SubItems[4].Text = reader.WaveFormat.ToString();
                    reader.Close();
                }
                catch
                {
                    liv.BackColor = ErrorMarkItemBackColor;
                }
            }
            resetRandom();
        }
    }
}
