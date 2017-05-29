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
    public enum PlayMod
    {
        Default, 列表播放, 列表循环, 随机播放, 单曲循环
    }
    public partial class Form1 : Form
    {
        public double mp3UnitLength = 176420.9;
        public string xmlFileName = "PlayRecord.xml";
        public string timeFormat = @"hh\:mm\:ss";
        public string percentFormat = "0.00%";
        public int noResponseTimeLimit = 5000; //无响应时间上限 //TODO:待修改
        public float defaultVolum = 0.6f;

        public Color PlayingItemBackColor = Color.LightGreen;
        public Color PauseItemBackColor = Color.Yellow;
        public Color StopItemBackColor = Color.SkyBlue;
        public Color DefaultItemBackColor = Color.White;
        public Color ErrorItemBackColor = Color.PaleVioletRed;
        public Color ErrorMarkItemBackColor = Color.Pink;
        public Color NoFileMarkItemBackColor = Color.DarkGray;
        public Color NoFileItemBackColor = Color.Gray;

        private PlayMod playMod = PlayMod.Default;
        private bool stopByHand = false;
        private int noResponseTime = 0; //TODO:待修改

        private ListViewItem CurrentItem
        { get => currentPlayHistoryNode?.Value; }
        private LinkedList<ListViewItem> playHistory;
        private LinkedListNode<ListViewItem> currentPlayHistoryNode = null;

        private XMLDataManager xmlManager;
        private NoRepeatRandom random;

        Mp3FileReader fileReader;
        WaveStream wavStream;
        private BlockAlignReductionStream baStream;
        private WaveOut waveOutDevice;


        public Form1()
        {
            InitializeComponent();
            无ToolStripMenuItem.Tag = PlayMod.Default;
            列表循环ToolStripMenuItem.Tag = PlayMod.列表循环;
            单曲循环ToolStripMenuItem.Tag = PlayMod.单曲循环;
            顺序ToolStripMenuItem.Tag = PlayMod.列表播放;
            随机播放ToolStripMenuItem.Tag = PlayMod.随机播放;

            playHistory = new LinkedList<ListViewItem>();
            waveOutDevice = new WaveOut(WaveCallbackInfo.FunctionCallback());
            waveOutDevice.PlaybackStopped += WaveOutDevice_PlaybackStopped;
            waveOutDevice.Volume = defaultVolum;
            xmlManager = new XMLDataManager(xmlFileName);

            try
            {
                xmlManager.loadData(out Dictionary<string, string> globalData, out List<Dictionary<string, string>> itemsData);
                //载入条目数据
                loadItemsData(itemsData);
                //载入全局数据
                loadGlobalData(globalData);
            }
            catch (Exception ex)
            {
                MessageBox.Show("XMl文件载入错误，创建新文件（旧文件将自动备份）\n\n错误信息：" + ex.Message);
                FileInfo xmlFile = new FileInfo(xmlFileName);
                try
                {
                    File.Copy(xmlFile.FullName, xmlFile.DirectoryName + "//[备份：错误的记录文件]" + xmlFile.Name, true);
                }
                catch { }
            }

            if (CurrentItem != null)
            {
                playAudio(CurrentItem);
            }

        }

        private void loadGlobalData(Dictionary<string, string> globalData)
        {
            if (int.TryParse(loadAttribute(globalData, "当前索引"), out int currentPlayIndex))
            {
                if (currentPlayIndex != -1 && currentPlayIndex < listView1.Items.Count)
                {
                    currentPlayHistoryNode = playHistory.AddFirst(listView1.Items[currentPlayIndex]);
                    switch (CurrentItem.SubItems[1].Tag as string)
                    {
                        case "可播":
                            CurrentItem.BackColor = StopItemBackColor;
                            break;
                        case "不可播":
                            CurrentItem.BackColor = ErrorItemBackColor;
                            break;
                        case "文件丢失":
                        default:
                            CurrentItem.BackColor = NoFileItemBackColor;
                            break;
                    }
                }
            }
        }

        private string loadAttribute(Dictionary<string, string> data, string attributeName, string defualtStr = "-")
        {
            if (!data.TryGetValue(attributeName, out string value))
            {
                value = defualtStr;
                //MessageBox.Show(string.Format("属性\"{0}\"未能载入\n", attributeName),
                //  "XML文件载入错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return value;
        }

        private void loadItemsData(List<Dictionary<string, string>> dataList)
        {
            //载入所有项目
            foreach (var data in dataList)
            {
                FileInfo fileInfo = new FileInfo(loadAttribute(data, "文件路径"));

                ListViewItem lvi = listView1.Items.Add(fileInfo.Name);
                lvi.Tag = fileInfo;

                lvi.SubItems.Add("-");
                lvi.SubItems.Add("-");
                lvi.SubItems.Add("-");
                lvi.SubItems.Add("-");

                if (!fileInfo.Exists)
                {
                    lvi.BackColor = NoFileMarkItemBackColor;
                    continue;
                }

                //播放到
                lvi.SubItems[1].Text = regularizeAsTime(loadAttribute(data, "播放时间"));
                //总时长
                lvi.SubItems[2].Text = regularizeAsTime(loadAttribute(data, "总时长"));
                //百分比
                TimeSpan.TryParse(loadAttribute(data, "播放时间"), out TimeSpan timeRecord);
                TimeSpan.TryParse(loadAttribute(data, "总时长"), out TimeSpan totalTime);
                lvi.SubItems[3].Text =
                    timeRecord == TimeSpan.Zero || totalTime == TimeSpan.Zero ?
                    "-" : (timeRecord.TotalMilliseconds / totalTime.TotalMilliseconds).ToString(percentFormat);
                //音量
                float.TryParse(loadAttribute(data, "音量", defaultVolum.ToString()), out float volum);
                lvi.SubItems[2].Tag = volum;
                //附加信息
                lvi.SubItems[4].Text = regularizeAsText(loadAttribute(data, "附加信息"));

                //文件状态
                lvi.SubItems[1].Tag = loadAttribute(data, "文件状态");
                switch (lvi.SubItems[1].Tag as string)
                {
                    case "不可播":
                        lvi.BackColor = ErrorMarkItemBackColor;
                        break;
                    case "文件丢失":
                        lvi.BackColor = NoFileMarkItemBackColor;
                        break;
                    case "可播":
                    case "未知":
                    default:
                        lvi.BackColor = DefaultItemBackColor;
                        break;
                }
            }
            //重置不重复随机数发生器
            resetRandom();
        }

        private string regularizeAsTime(string timeString)
        {
            TimeSpan ts;
            TimeSpan.TryParse(timeString, out ts);
            return ts == TimeSpan.Zero ? "-" : ts.ToString(timeFormat);
        }
        private string regularizeAsText(string text)
        {
            //text = text.Trim(); //??
            return text == "" ? "-" : text;
        }
        private TimeSpan parsePositionToTime(long position)
        {
            return TimeSpan.FromSeconds(position / mp3UnitLength);
        }

        private void resetRandom()
        {
            random = new NoRepeatRandom(0, listView1.Items.Count);
        }
        private void WaveOutDevice_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            //if (rdr.Position == rdr.Length ) 
            //上句判断中,不知为何播放完成后position经常不到流结尾(已改为由stopPlayByHand标记变量判断)

            if (!stopByHand) //播放完一曲
            {
                updateUI();//用于自动停止
                自动续播();
            }
            else
            {
                stopByHand = false;
            }
        }

        private void 自动续播()
        {
            switch (playMod)
            {
                case PlayMod.Default:
                    break;
                case PlayMod.列表播放:
                    if (CurrentItem.Index == listView1.Items.Count - 1)
                    {
                        break;
                    }
                    playNext();
                    break;
                case PlayMod.列表循环:
                    playNext();
                    break;
                case PlayMod.随机播放:
                    PlayAudioByRandom();
                    break;
                case PlayMod.单曲循环:
                    playAudio(CurrentItem);
                    break;
                default:
                    break;
            }

        }

        private void trackBar_process_Scroll(object sender, EventArgs e)
        {
            if (waveOutDevice.PlaybackState != PlaybackState.Stopped)
            {
                long positionUnitSize = baStream.Length / trackBar_process.Maximum;
                long position = trackBar_process.Value * positionUnitSize;
                position = position / baStream.BlockAlign * baStream.BlockAlign; //块对齐
                baStream.Position = position;
            }
        }

        private void playAudio(ListViewItem item, bool recordOn = true, int autoNextDirection = 1)
        {
            ////若播放项目与当前正在播放的项目相同则返回
            //if (waveOutDevice.PlaybackState == PlaybackState.Playing && CurrentItem == item)
            //{
            //    return;
            //}
            //停止之前的播放
            if (waveOutDevice.PlaybackState != PlaybackState.Stopped)
            {
                waveOutDevice.PlaybackStopped -= WaveOutDevice_PlaybackStopped;
                waveOutDevice.Stop();
            }
            //添加到历史表
            if (recordOn && currentPlayHistoryNode == null)
            {
                currentPlayHistoryNode = playHistory.AddFirst(item);
            }
            else if (recordOn && CurrentItem != item) //排除停止后再次播放的情况
            {
                while (playHistory.Last != null && currentPlayHistoryNode != playHistory.Last) //删除当前节点后的所有节点
                    playHistory.RemoveLast();
                currentPlayHistoryNode = playHistory.AddAfter(currentPlayHistoryNode, item);
            }
            //获取已播放时间
            FileInfo fileInfo = item.Tag as FileInfo;
            TimeSpan.TryParse(item.SubItems[1].Text, out TimeSpan timePosition);
            if (fileInfo.Exists)
            {
                //从记录的播放时间开始播放
                try
                {
                    loadAudioFile(fileInfo.FullName, timePosition);
                    waveOutDevice.Play();

                    //以下内容建立在可以播放的前提下
                    item.SubItems[1].Tag = "可播";

                    //获取音量
                    float? volum = item.SubItems[2].Tag as float?;
                    waveOutDevice.Volume = volum != null && volum >= 0 && volum <= 1 ?
                        volum.Value : defaultVolum;
                }
                catch
                {
                    item.SubItems[1].Tag = "不可播";
                    autoSkipErrorItem(recordOn ? autoNextDirection : 0);
                }
            }
            else
            {
                item.SubItems[1].Tag = "文件丢失";
                autoSkipErrorItem(recordOn ? autoNextDirection : 0);
            }
            updateUI(); //用于播放
        }

        private void autoSkipErrorItem(int autoNextDirection)
        {
            //timer_noResponseMonitor.Start(); //TODO:待修改
            handPlayError(autoNextDirection);
            //timer_noResponseMonitor.Stop();  //TODO:待修改
            //noResponseTime = 0;  //TODO:待修改
        }

        private void handPlayError(int autoNextDirection)
        {
            if (playMod != PlayMod.Default && playMod != PlayMod.单曲循环 && autoNextDirection != 0)
            {
                if (noResponseTime < noResponseTimeLimit) //TODO:待修改
                {
                    updateUI();
                    CurrentItem.Text = noResponseTime.ToString();
                    int nextIndex = CurrentItem.Index + autoNextDirection;
                    if (nextIndex >= listView1.Items.Count && playMod != PlayMod.列表播放)
                    {
                        nextIndex = 0;
                    }
                    else if (nextIndex < 0 && playMod != PlayMod.列表播放)
                    {
                        nextIndex = listView1.Items.Count;
                    }
                    if (nextIndex >= 0 && nextIndex < listView1.Items.Count)
                    {
                        playAudio(listView1.Items[nextIndex], true, autoNextDirection); //此处递归调用使 skipErrorFileCount 增加
                    }
                }
                else //TODO:待修改
                {
                    MessageBox.Show("由于在自动播放下一文件的任务中，长时间未能打开一个文件，故已暂停自动续播下一首。"
                        , "自动续播暂停提示"
                        , MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void playNext()
        {
            if (CurrentItem == null)
            {
                return;
            }
            int nextIndex = CurrentItem.Index + 1;
            switch (playMod)
            {
                case PlayMod.Default:
                case PlayMod.列表播放:
                case PlayMod.单曲循环:
                    if (nextIndex < listView1.Items.Count)
                    {
                        playAudio(listView1.Items[nextIndex]);
                    }
                    break;
                case PlayMod.列表循环:
                    if (nextIndex >= listView1.Items.Count)
                    {
                        nextIndex = 0;
                    }
                    playAudio(listView1.Items[nextIndex]);
                    break;
                case PlayMod.随机播放:
                    PlayAudioByRandom();
                    break;

                default:
                    break;
            }
        }

        private void PlayAudioByRandom()
        {
            try
            {
                playAudio(listView1.Items[random.Next()]);

            }
            catch (ApplicationException)
            {
                random.reset();
                PlayAudioByRandom();
            }
        }
        private void loadAudioFile(String path, TimeSpan timePosition)
        {
            //释放上一次加载的文件流
            //if (baStream != null)
            //{
            //    fileReader.Dispose();
            //    wavStream.Dispose();
            //    baStream.Dispose();
            //}
            waveOutDevice.Dispose(); //这个对内存释放起决定性作用

            fileReader = new Mp3FileReader(path);
            wavStream = WaveFormatConversionStream.CreatePcmStream(fileReader);
            baStream = new BlockAlignReductionStream(wavStream);
            waveOutDevice = new WaveOut();
            waveOutDevice.Init(baStream);
            waveOutDevice.PlaybackStopped += WaveOutDevice_PlaybackStopped;
            baStream.CurrentTime = timePosition;
        }

        private void timerTrackBar_Tick(object sender, EventArgs e)
        {
            double timeUnitSize = baStream.TotalTime.TotalMilliseconds / trackBar_process.Maximum;
            int value = (int)(baStream.CurrentTime.TotalMilliseconds / timeUnitSize);
            trackBar_process.Value = value > trackBar_process.Maximum ? trackBar_process.Maximum : value;
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
                item.SubItems[2].Tag = defaultVolum;
                //Mp3FileReader reader = new Mp3FileReader(fileInfo.FullName);

                //item.SubItems[2].Text = "555";//reader.TotalTime.ToString(timeFormat);
                //item.SubItems[4].Text = reader.WaveFormat.ToString();

                // catch
                //{
                //    item.BackColor = ErrorMarkItemBackColor;
                // }
            }
            resetRandom();
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            playFocusedItem();
        }

        private void playFocusedItem()
        {
            if (currentPlayHistoryNode != null && listView1.FocusedItem == CurrentItem
                && waveOutDevice.PlaybackState == PlaybackState.Paused)
            {
                waveOutDevice.Resume();
                updateUI(); //用于暂停恢复
            }
            else
            {
                playAudio(listView1.FocusedItem, true, 0); //播放焦点项目不自动移动
            }
            listView1.FocusedItem.Selected = false;

        }

        private void updateUI(bool 播放记录逆向移动 = false) //更新所有UI，但只更新列表中当前播放条目和上一次播放的条目
        {
            if (CurrentItem == null)
            {
                return;
            }
            //处理上一个条目的显示
            ListViewItem lastItem = null;
            if (播放记录逆向移动) //用于后退
            {
                lastItem = currentPlayHistoryNode.Next.Value;
            }
            else if (currentPlayHistoryNode.Previous != null)
            {
                lastItem = currentPlayHistoryNode.Previous.Value;
            }
            if (lastItem != null)
            {
                switch (lastItem.SubItems[1].Tag as string)
                {
                    case "可播":
                        lastItem.BackColor = DefaultItemBackColor;
                        break;
                    case "不可播":
                        lastItem.BackColor = ErrorMarkItemBackColor;
                        break;
                    case "文件丢失":
                    default:
                        lastItem.BackColor = NoFileMarkItemBackColor;
                        break;
                }
            }

            //处理当前条目和进度条、按钮、音量条的显示
            switch (waveOutDevice.PlaybackState)
            {
                case PlaybackState.Playing:
                    buttonPlayPause.Text = "||";
                    CurrentItem.BackColor = PlayingItemBackColor;
                    CurrentItem.SubItems[1].Tag = "可播";
                    CurrentItem.SubItems[2].Text = baStream.TotalTime.ToString(timeFormat);
                    CurrentItem.Focused = true;
                    trackBar_volum.Value = (int)(trackBar_volum.Maximum * (CurrentItem.SubItems[2].Tag as float?));
                    //listView1.AutoScrollOffset = CurrentItem.Position;
                    timerStart();
                    break;
                case PlaybackState.Paused:
                    timerStop();
                    buttonPlayPause.Text = ">";
                    CurrentItem.BackColor = PauseItemBackColor;
                    break;
                case PlaybackState.Stopped:
                    timerStop();
                    buttonPlayPause.Text = ">";
                    switch (CurrentItem.SubItems[1].Tag as string)
                    {
                        case "可播":
                            CurrentItem.BackColor = StopItemBackColor;
                            break;
                        case "不可播":
                            CurrentItem.BackColor = ErrorItemBackColor;
                            break;
                        case "文件丢失":
                        default:
                            CurrentItem.BackColor = NoFileItemBackColor;
                            break;
                    }
                    trackBar_process.Value = 0;
                    CurrentItem.SubItems[1].Text = "-";
                    CurrentItem.SubItems[3].Text = "-";
                    break;
                default:
                    break;
            }
        }

        private void timerStop()
        {
            timerTrackBar.Stop();
            timerListview.Stop();
        }

        private void timerStart()
        {
            timerTrackBar.Start();
            timerListview.Start();
        }

        private void buttonPlayPause_Click(object sender, EventArgs e)
        {
            pausePlay();
        }

        private void pausePlay()
        {
            switch (waveOutDevice.PlaybackState)
            {
                case PlaybackState.Stopped:
                    if (listView1.FocusedItem != null)
                    {
                        playAudio(listView1.FocusedItem);
                        listView1.FocusedItem.Focused = false;
                    }
                    else if (listView1.Items.Count != 0)
                    {
                        if (currentPlayHistoryNode == null)
                        {
                            currentPlayHistoryNode = playHistory.AddFirst(listView1.Items[0]);
                        }
                        playAudio(CurrentItem);
                    }
                    break;
                case PlaybackState.Playing:
                    waveOutDevice.Pause();
                    break;
                case PlaybackState.Paused:
                    waveOutDevice.Resume();
                    break;
            }
            updateUI();//用于暂停
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            stopPlay();
        }

        private void stopPlay()
        {
            if (waveOutDevice.PlaybackState != PlaybackState.Stopped)
            {
                stopByHand = true;
                waveOutDevice.Stop(); //此句会触发 WaveOutDevice_PlaybackStopped 函数
                updateUI(); //用于手动停止
            }
        }

        private void buttonNext_Click(object sender, EventArgs e)
        {
            playNext();
        }

        private void buttonLast_Click(object sender, EventArgs e)
        {
            playLast();
        }

        private void playLast()
        {
            if (CurrentItem == null)
            {
                return;
            }
            int lastIndex = CurrentItem.Index - 1;
            switch (playMod)
            {
                case PlayMod.Default:
                case PlayMod.列表播放:
                case PlayMod.单曲循环:
                    if (lastIndex >= 0)
                    {
                        playAudio(listView1.Items[lastIndex], true, -1);
                    }
                    break;
                case PlayMod.列表循环:
                    if (lastIndex < 0)
                    {
                        lastIndex = listView1.Items.Count - 1;
                    }
                    playAudio(listView1.Items[lastIndex], true, -1);
                    break;
                case PlayMod.随机播放:
                    back();
                    break;
                default:
                    break;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            waveOutDevice.PlaybackStopped -= WaveOutDevice_PlaybackStopped;
            waveOutDevice.Stop();

            Dictionary<string, string> globalData;
            List<Dictionary<string, string>> itemsData;
            getDataFromListView(listView1, out globalData, out itemsData);
            try
            {
                xmlManager.save(globalData, itemsData);
            }
            catch (Exception ex)
            {
                MessageBox.Show("XML文件保存失败！ 错误信息：\n" + ex.Message);
            }
        }

        private void getDataFromListView(ListView listView, out Dictionary<string, string> globalData, out List<Dictionary<string, string>> itemsData)
        {
            globalData = new Dictionary<string, string>();
            itemsData = new List<Dictionary<string, string>>();

            globalData.Add("当前索引", CurrentItem == null ? "-" : CurrentItem.Index.ToString());

            foreach (ListViewItem item in listView.Items)
            {
                FileInfo fileInfo = item.Tag as FileInfo;
                //音量
                float? v = item.SubItems[2].Tag as float?;
                float volum = v != null && v >= 0 && v <= 1 ?
                    v.Value : defaultVolum;
                //文件状态
                string fileState = item.SubItems[1].Tag as string;
                if (fileState == null)
                {
                    fileState = "未知";
                }
                Dictionary<string, string> attributes = new Dictionary<string, string>
                {
                    ["文件路径"] = fileInfo.FullName,
                    ["播放时间"] = item.SubItems[1].Text,
                    ["总时长"] = item.SubItems[2].Text,
                    ["附加信息"] = item.SubItems[4].Text,
                    ["文件状态"] = fileState,
                    ["音量"] = volum.ToString()
                };
                itemsData.Add(attributes);
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
            PlayMod? _playMod = item.Tag as PlayMod?;
            playMod = _playMod == null ? PlayMod.Default : _playMod.Value;
        }

        private void 从列表中删除ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                while (playHistory.Remove(item)) { }; //删除所有该项
                item.Remove();
            }
            //如果删除了正在播放的项
            if (CurrentItem != null && CurrentItem.Index == -1)
            {
                stopPlay();
                currentPlayHistoryNode = playHistory.Last;
            }
            resetRandom();
        }

        private void buttonBack_Click(object sender, EventArgs e)
        {
            back();
        }

        private void back()
        {
            if (currentPlayHistoryNode == null || currentPlayHistoryNode.Previous == null)
            {
                return;
            }
            currentPlayHistoryNode = currentPlayHistoryNode.Previous;//前移
            playAudio(CurrentItem, false);
            updateUI(true);
        }

        private void buttonForward_Click(object sender, EventArgs e)
        {
            forward();
        }

        private void forward()
        {
            if (currentPlayHistoryNode == null || currentPlayHistoryNode.Next == null)
            {
                return;
            }
            currentPlayHistoryNode = currentPlayHistoryNode.Next;//前移
            playAudio(CurrentItem, false);
            updateUI();
        }

        private void 播放ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            playFocusedItem();
        }

        private void timerListview_Tick(object sender, EventArgs e)
        {
            CurrentItem.SubItems[1].Text = baStream.CurrentTime.ToString(timeFormat);
            CurrentItem.SubItems[3].Text = ((float)baStream.Position / baStream.Length).ToString(percentFormat);
        }

        private void trackBar_volum_Scroll(object sender, EventArgs e)
        {
            if (waveOutDevice.PlaybackState == PlaybackState.Stopped)
            {
                return;
            }
            float volum = (float)trackBar_volum.Value / trackBar_volum.Maximum;
            waveOutDevice.Volume = volum;
            CurrentItem.SubItems[2].Tag = volum;
        }

        private void timer_noResponseMonitor_Tick(object sender, EventArgs e)
        {
            noResponseTime += timer_noResponseMonitor.Interval; //TODO:待修改
        }
    }
}
