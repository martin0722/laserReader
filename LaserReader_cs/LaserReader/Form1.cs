using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using Tao.OpenGl;
using Tao.Platform.Windows;

namespace LaserReader
{
    public partial class Form1 : Form
    {
        //Chance the content in elements
        delegate void SetTextCallback(string text);

        //Timer
        Stopwatch sw = new Stopwatch();
        long time;

        //Ethernet
        TcpClient client;
        NetworkStream ns;
        Thread t = null;
        int portNum;
        string hostName;
        bool isconnect = false;

        //Kalman used
        int[] dist, dist_old;
        double[] P, K; 

        //Record data to .txt
        bool isRecord = false;
        String Data = null;
        String fileName = null;

        public Form1()
        {
            InitializeComponent();
            textBox2.Text = "sRN LMDscandata ";
            textBox3.Text = "192.168.1.203";
            textBox4.Text = "2112";           
        }

        private void button1_Click(object sender, EventArgs e)
        {
            isconnect = !isconnect;
            
            //Set ethernet parameters
            hostName = textBox3.Text;
            try
            {
                portNum = Int32.Parse(textBox4.Text);
            }
            catch(FormatException exp)
            {
                MessageBox.Show(exp.ToString());
                button1.Text = "Connect";
                textBox1.Text = "";
                isconnect = false;
            }           
           
            //Set Kalman parameters
            dist_old = new int[541];
            P = new double[541];
            K = new double[541];

            if (isconnect)
            {
                try
                {
                    client = new TcpClient(hostName, portNum);
                    ns = client.GetStream();
                    t = new Thread(DoWork);
                    t.Start();
                    button1.Text = "Disconnect";
                }
                catch (SocketException exp)
                {
                    MessageBox.Show(exp.ToString());
                    button1.Text = "Connect";
                    textBox1.Text = "";
                }
                catch (ArgumentNullException exp)
                {
                    MessageBox.Show(exp.ToString());
                    button1.Text = "Connect";
                    textBox1.Text = "";
                }
            }
            else
            {
                button1.Text = "Connect";
                textBox1.Text = "";
                simpleOpenGlControl1.Invalidate();
                try
                {
                    client.Close();
                    ns.Close();
                }
                catch(NullReferenceException exp)
                {
                    MessageBox.Show(exp.ToString());
                }
                
                //End record at the end of connect if is recording
                if (isRecord)
                {
                    button3.Text = "Start Record";
                    fileName = DateTime.Now.ToString("yyyy_MM_dd_hh_mm_ss");
                    System.IO.StreamWriter file = new System.IO.StreamWriter(fileName + ".txt");
                    file.WriteLine(Data);
                    file.Close();
                    Data = null;
                }
            }                               
        }

        //Main thread
        public void DoWork()
        {           
            byte[] bytes = new byte[3000]; 
            while (isconnect)
            {
                sw.Start();
                String s = textBox2.Text;
                byte[] byteTime = Encoding.ASCII.GetBytes(s);

                try
                {
                    ns.Write(byteTime, 0, byteTime.Length);
                    int bytesRead = ns.Read(bytes, 0, bytes.Length);
                    dist = HexToDistance(Encoding.ASCII.GetString(bytes, 0, bytesRead));
                    
                    //Kalman Filter
                    for (int i = 0; i <= 540; i++)
                    {
                        P[i] = P[i] + 1.0;
                        K[i] = P[i] / (P[i] + (double)numericUpDown2.Value);
                        dist[i] = dist_old[i] + (int)(K[i] * (dist[i] - dist_old[i]));
                        P[i] = (1.0 - K[i]) * P[i];
                    }
                    dist_old = dist;

                    //convert distance(int) to string
                    string text = string.Join(", ", dist);

                    //clear figure and redraw
                    simpleOpenGlControl1.Invalidate();

                    //Start record data
                    if (isRecord)
                    {                        
                        Data = Data + text + "\n";                        
                    }
                      
                    //Show data in hex or dec
                    if (checkBox1.Checked)
                    {                     
                        this.SetText(text);
                    }
                    else
                    {
                        this.SetText(Encoding.ASCII.GetString(bytes, 0, bytesRead));
                    }                   
                }
                catch(ObjectDisposedException exp)
                {
                    MessageBox.Show(exp.ToString());
                    SetButtonText("Connect");
                    SetText("");
                }
                catch(System.IO.IOException exp)
                {
                    MessageBox.Show(exp.ToString());
                    SetButtonText("Connect");
                    SetText("");
                }

                //Check data length
                if (textBox1.Text.Length < 500 && isconnect)
                {
                    MessageBox.Show("Wrong Input Data to Laser!");
                    isconnect = false;
                    client.Close();
                    ns.Close();
                    SetButtonText("Connect");
                }

                sw.Stop();
                time = sw.ElapsedMilliseconds;

                //Wait until 20ms
                if (20 - time > 0)
                {
                    Thread.Sleep((int)(20-time));
                }                
            }
        }

        private void SetText(string text)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (this.textBox1.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.textBox1.Text = text;
            }
        }

        private void SetButtonText(string text)
        {
            if (this.button1.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetButtonText);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.button1.Text = text;
            }
        }

        public int[] HexToDistance(string hex)
        {
            string[] data = hex.Split(' ');  
            int[] dist;
            dist = new int[541];
            for (int i = 0; i <= 540; i++)
            {
                dist[i] = Convert.ToInt32(data[i+26], 16);//The distance we want aftre 26th element in data
            }
            return dist;
        }

        //Data record function
        private void button3_Click(object sender, EventArgs e)
        {
            if (isconnect)
            {
                isRecord = !isRecord;
                if (isRecord)
                {
                    button3.Text = "Stop Record";
                }
                else
                {
                    button3.Text = "Start Record";
                    fileName = DateTime.Now.ToString("yyyy_MM_dd_hh_mm_ss");
                    System.IO.StreamWriter file = new System.IO.StreamWriter(fileName + ".txt");
                    file.WriteLine(Data);
                    file.Close();
                }
            }
            else
            {
                MessageBox.Show("Please connect the device first.");
            }          
        }

        //Show data in textBox
        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked)
            {
                textBox1.Width = 700;
            }
            else
            {
                textBox1.Width = 254;
            }
        }

        //initialize opengl setting
        private void Form1_Load(object sender, EventArgs e)
        {
            int height = simpleOpenGlControl1.Height;
            int width = simpleOpenGlControl1.Width;
            simpleOpenGlControl1.InitializeContexts();
            Gl.glViewport(0, 0, width, height);
            Gl.glMatrixMode(Gl.GL_PROJECTION);
            Gl.glLoadIdentity();
            Glu.gluPerspective(45.0f, (double)width / (double)height, 0.01f, 5000.0f);
        }


        private void simpleOpenGlControl1_Paint(object sender, PaintEventArgs e)
        {
            Gl.glClear(Gl.GL_COLOR_BUFFER_BIT | Gl.GL_DEPTH_BUFFER_BIT); //clear buffers to preset values
            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glLoadIdentity();                 // load the identity matrix

            //world frame = 0.414
            double x_tran, y_tran;
            int x_n, y_n, x_p, y_p;
            x_tran = ((double)hScrollBar1.Value / 45.0 - 1.0) * ((double)numericUpDown1.Value - 1.0) * 0.414;
            y_tran = (1.0 - (double)vScrollBar1.Value / 45.0) * ((double)numericUpDown1.Value - 1.0) * 0.414;

            x_n = (int)((-20000.0 + x_tran / 0.414 * 20000.0) / (double)numericUpDown1.Value);
            y_n = (int)((-20000.0 + y_tran / 0.414 * 20000.0) / (double)numericUpDown1.Value);
            x_p = (int)((20000.0 + x_tran / 0.414 * 20000.0) / (double)numericUpDown1.Value);
            y_p = (int)((20000.0 + y_tran / 0.414 * 20000.0) / (double)numericUpDown1.Value);
            xn.Text = x_n.ToString();
            yn.Text = y_n.ToString();
            xp.Text = x_p.ToString();
            yp.Text = y_p.ToString();

            Gl.glBegin(Gl.GL_TRIANGLES);
            Gl.glColor3d(1, 0, 0);
            Gl.glVertex3d(0 - x_tran, 0.01 - y_tran, -1);
            Gl.glVertex3d(0.0075 - x_tran, -0.005 - y_tran, -1);
            Gl.glVertex3d(-0.0075 - x_tran, -0.005 - y_tran, -1);
            Gl.glEnd();

            if (isconnect)
            {
                Gl.glPointSize(3);
                Gl.glBegin(Gl.GL_POINTS); 
                Gl.glColor3d(1, 1, 1);
                double x, y;
                for (int i = 0; i <= 540; i++)
                {
                    x = ((double)dist[i] * (double)numericUpDown1.Value * Math.Cos(Math.PI * (-45.0 + 0.5 * (double)i) / 180.0) * 0.414 / 20000.0) - x_tran;
                    y = ((double)dist[i] * (double)numericUpDown1.Value * Math.Sin(Math.PI * (-45.0 + 0.5 * (double)i) / 180.0) * 0.414 / 20000.0) - y_tran;
                    Gl.glVertex3d(x, y, -1);
                }
                Gl.glEnd();
            }                
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (isconnect)
            {
                client.Close();
                ns.Close();
            }
            if (isRecord)
            {
                fileName = DateTime.Now.ToString("yyyy_MM_dd_hh_mm_ss");
                System.IO.StreamWriter file = new System.IO.StreamWriter(fileName + ".txt");
                file.WriteLine(Data);
                file.Close();
                Data = null;
            }
        }
    }
}