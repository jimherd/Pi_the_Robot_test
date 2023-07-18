//
//
//

using System;
using System.IO.Ports;
using System.Threading;
using System.Windows.Forms;
using static Pi_the_Robot_test.Command_IO;

namespace Pi_the_Robot_test {

    public partial class Form1 : Form {

        private const bool Debug = true;

        //***********************************************************************
        // System data
        //***********************************************************************

        int Nos_servos   = 0;
        int Nos_steppers = 0;

        //***********************************************************************
        // Constant definitions
        //***********************************************************************

        //private const int SUCCESS = 0;

        private const int DEFAULT_PORT = 9;

        string com_port;
        private string[] baud_rates = new string[] { "115200", "256000", "230400", "128000", "9600" };
        private string[] stepper_commands = new string[] { "Relative move", "Absolute move", "Relative move sync",
                                                                "Absolute move sync", "Calibrate"};
        private string[] profile_commands = new string[] { "fast", "medium", "slow" };
        private string[] stepper_motors   = new string[] { "Neck" };
        private string[] info_commands    = new string[] { "System", "Servo", "Stepper motor"};

        private const double MIN_PWM_FREQUENCY = 0.01;   //kHz
        private const double MAX_PWM_FREQUENCY = 100.0;  //kHz

        const int SUCCESS = 0;
        const int COMBAUD = 115200;
        const int READ_TIMEOUT = 10000;   // timeout for read reply (10 seconds)

        const int MAX_CMD_TRIES = 10;

        //***********************************************************************
        // variables and methods
        //***********************************************************************

        private Boolean servo_speed_move_enabled =false;
        private Boolean servo_sync_move_enabled = false;
        private Boolean connected = false;
        private UInt32 global_error;

        //***********************************************************************
        //  Initialise oblects
        //***********************************************************************

        public Command_IO Command_IO = new Command_IO();   // import Command_IO functions
        
        private static Mutex mutex = new Mutex();

        System.Random random = new System.Random();

        public Form1()
        {
            InitializeComponent();
        }

        

        //***********************************************************
        // User functions
        //***********************************************************


        //***********************************************************
        // Window interface functions
        //***********************************************************

        public void DebugPrint(string message)
        {
            DebugWindow.AppendText(message + Environment.NewLine);
        }

        public void InfoPrint(string message)
        {
            InfoWindow.AppendText(message + Environment.NewLine);
        }

        //***********************************************************************
        // User functions
        //*********************************************************************** 
        

        //***********************************************************************
        // Window interface functions
        //*********************************************************************** 

        private void Form1_Load(object sender, EventArgs e)
        {
            // 1. Populate Serial port combobox
            // 2. Initialise baud rate combobox
            // 3. Initialse global variables
            // 4. Sleep for a couple of seconds 

            comboBox1.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length > 0) {
                foreach (string s in ports) {
                    comboBox1.Items.Add(s);
                }
            }
            else {
                InfoWindow.AppendText("No serial ports" + Environment.NewLine);
                return;
            }
            comboBox1.SelectedIndex = 0;

            //
            comboBox2.DataSource = baud_rates;
            comboBox2.SelectedIndex = 0;
            comboBox3.DataSource = stepper_commands;
            comboBox3.SelectedIndex = 0;
            comboBox4.DataSource = profile_commands;
            comboBox4.SelectedIndex = 0;
            comboBox5.DataSource = stepper_motors;
            comboBox5.SelectedIndex = 0;
            comboBox6.DataSource = info_commands;
            comboBox6.SelectedIndex = 0;
            //

            serialPort1.BaudRate = COMBAUD;

            connected = false;
            button1.Enabled = true;
            button2.Enabled = false;
            button3.Enabled = false;
            button4.Enabled = false;
            button9.Enabled = false;
            button10.Enabled = false;
            button12.Enabled = false;

            global_error = SUCCESS;
            Thread.Sleep(2000);
        }

        //***********************************************************
        // Open_COM_port : Open selected seial COM port
        // =============
        private void Open_COM_port(object sender, EventArgs e)
        {
            Command_IO.ErrorCode status = Command_IO.ErrorCode.OK;

            if (comboBox1.SelectedItem == null) {
                InfoWindow.AppendText("No COM port selected " + Environment.NewLine);
                return;
            }
            com_port = comboBox1.SelectedItem.ToString();
            int baud_rate = Convert.ToInt32(comboBox2.SelectedItem);

            status = Command_IO.open_comms(com_port, baud_rate);

            if (status != Command_IO.ErrorCode.OK) {
                InfoWindow.AppendText("Cannot open " + com_port + Environment.NewLine);
                return;
            }
            connected = true;
            button2.Enabled = true;
            button3.Enabled = true;
            button4.Enabled = true;
            button9.Enabled = true;
            button10.Enabled = true;
            button12.Enabled = true;
            button1.Enabled = false;
            // timer1.Enabled = true;

            InfoWindow.AppendText(com_port + " now open" + Environment.NewLine);
        }

        //***********************************************************
        private void exitToolStripMenuItem_Click_2(object sender, EventArgs e)
        {
            serialPort1.Close();
            this.Close();
        }

        private void aboutToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            MessageBox.Show("C# program to test servos/motor on Pi the Robot head");
        }

        private void Clear_INFO_window(object sender, EventArgs e)
        {
            InfoWindow.Clear();
        }

        //****************************************************************
        // Run commands to PING FPGA/uP or read FPGA subsystem configuration

        private void Execute_PING(object sender, EventArgs e)
        {
            Command_IO.ErrorCode status = Command_IO.ErrorCode.OK;

            int data;

            int valueInt = Int32.Parse(textBox1.Text);
            InfoWindow.AppendText("Ping : Data =" + valueInt + Environment.NewLine);
            status = Command_IO.Ping(valueInt, out data);
            InfoWindow.AppendText("Ping command reply = " + Command_IO.reply_string + Environment.NewLine);
            InfoWindow.AppendText("Ping : Return code = " + status +  ":: Data = " + data + Environment.NewLine);
            textBox1.Text = Convert.ToString(random.Next(1, 99));
        }

        //****************************************************************
        // Run commands to execute RC servo commands
        private void Execute_RC_servo_cmd(object sender, EventArgs e)
        {
            Command_IO.ErrorCode status = Command_IO.ErrorCode.OK;
            int data;
            Command_IO.ServoCommands servo_command;

            int servo_no = (int)numericUpDown3.Value;
            int servo_angle = (int)numericUpDown4.Value;

            if (servo_speed_move_enabled == false) {
                servo_command = Command_IO.ServoCommands.ABS_MOVE;
                if (servo_sync_move_enabled == true) {
                    servo_command = Command_IO.ServoCommands.ABS_MOVE_SYNC;
                }
            } else {
                servo_command = Command_IO.ServoCommands.SPEED_MOVE;
                if (servo_sync_move_enabled == true) {
                    servo_command = Command_IO.ServoCommands.SPEED_MOVE_SYNC;
                }
            }
            Command_IO.command_string = "servo " + DEFAULT_PORT + " " + (int)servo_command + " " + servo_no + " " + servo_angle;
            switch (servo_command) {
                case Command_IO.ServoCommands.ABS_MOVE :
                case Command_IO.ServoCommands.ABS_MOVE_SYNC:
                    Command_IO.command_string += "\n";
                    break;
                case Command_IO.ServoCommands.SPEED_MOVE :
                case Command_IO.ServoCommands.SPEED_MOVE_SYNC :
                    Command_IO.command_string += " " + numericUpDown1.Value + "\n";
                    break;
                default:
                    break;
            }

            InfoWindow.AppendText("Servo command = " + Command_IO.command_string + Environment.NewLine);
            status = Command_IO.do_command(out data);
            InfoWindow.AppendText("Servo : Return code = " + status + ":: Data = " + data + Environment.NewLine);
        }

        private void Close__COM_port_Click(object sender, EventArgs e)
        {
            Command_IO.ErrorCode status = Command_IO.ErrorCode.OK;

            timer1.Enabled = false;
            status = Command_IO.Close_comms(com_port);

            if (status != Command_IO.ErrorCode.OK) {
                InfoWindow.AppendText("Cannot close COM port" + Environment.NewLine);
                return;
            }
            InfoWindow.AppendText("COM port now closed" + Environment.NewLine);

            connected = false;
            button1.Enabled = true;
            button2.Enabled = false;
            button3.Enabled = false;
            button4.Enabled = false;
            button9.Enabled = false;
            button10.Enabled = false;
            button12.Enabled = false;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Command_IO.command_string = "p 5 ";

            DebugWindow.AppendText(Command_IO.command_string + Environment.NewLine);
            serialPort1.WriteLine(Command_IO.command_string);
            DebugWindow.AppendText("Wating for reply" + Environment.NewLine);
            Command_IO.ErrorCode status = Command_IO.get_reply();
        }
        string[] sequence_1 = new[] {
            "ping 0 1\n",
            "servo 0 0 0 -45\n",   
        //    "delay 0 200\n",
            "servo 0 0 0 45\n",
        //    "delay 0 1000\n",
            "servo 0 2 0 -45 100\n",
        //    "delay 0 11000\n",
            "servo 0 2 0 45 30\n", 
        };
        string[] sequence_2 = new[] {
            "ping 0 2"
        };

        private void execute_sequence__Click(object sender, EventArgs e)
        {
            Command_IO.ErrorCode status;
            int data, count;

            status = ErrorCode.OK;
            if (Sequence2Button.Checked == true) {
                for (int i = 0; i < sequence_2.Length; i++) {
                    Command_IO.command_string = sequence_2[i];
                    InfoWindow.AppendText("Sequence cmd : " + Command_IO.command_string + Environment.NewLine);
                    status = (Command_IO.do_command(out data));
                    InfoWindow.AppendText("Sequence reply : " + Command_IO.reply_string + Environment.NewLine);
                    if (status != Command_IO.ErrorCode.OK) {
                        break;
                    }
                }
            }
            if (Sequence1Button.Checked == true) {
                InfoWindow.AppendText("Sequence   :" + Environment.NewLine);
                for (int i = 0; i < sequence_1.Length; i++) {
                    count = MAX_CMD_TRIES;
                    do {
                        Command_IO.command_string = sequence_1[i];
                        InfoWindow.AppendText("*");
                        status = (Command_IO.do_command(out data));
                        InfoWindow.AppendText("Cmd   : " + Command_IO.command_string + Environment.NewLine);
                        InfoWindow.AppendText("Reply : " + Command_IO.reply_string + Environment.NewLine);
                        count--;
                        if ((status == ErrorCode.SERVO_BUSY) || (status == ErrorCode.STEPPER_BUSY)) {
                            Thread.Sleep(1000);
                            continue;
                        } else {
                            if ((status == ErrorCode.OK)) {
                                break;
                            }
                            else {
                                break;  // fron do loop
                            }
                        }
                    } while (count > 0);
                    if (status != Command_IO.ErrorCode.OK) {
                        break;  // terminate sequence execution
                    }
                }
            }
            InfoWindow.AppendText("Sequence : Return code = " + status + Environment.NewLine);
            return;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked == true) {
                servo_speed_move_enabled = true;
                numericUpDown1.Enabled = true;
            } else {
                servo_speed_move_enabled = false;
                numericUpDown1.Enabled = false;
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked == true) {
                servo_sync_move_enabled = true;
            } else {
                servo_sync_move_enabled = false;
            }
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            int data;
            Command_IO.ErrorCode status = Command_IO.ErrorCode.OK;

            Command_IO.command_string = "sync " + DEFAULT_PORT + "\n";
            InfoWindow.AppendText("Servo command = " + Command_IO.command_string + Environment.NewLine);
            status = Command_IO.do_command(out data);
            InfoWindow.AppendText("Servo : Return code = " + status + ":: Data = " + data + Environment.NewLine);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Command_IO.ErrorCode status = Command_IO.ErrorCode.OK;
            int data;
            int stepper_command;
            int profile_command;
            int stepper_motor;

            //int stepper_no = (int)numericUpDown2.Value;
            int stepper_angle = (int)numericUpDown5.Value;

            // get stepper command

            stepper_motor = (int)comboBox5.SelectedIndex;
            stepper_command = (int)comboBox3.SelectedIndex;
            profile_command = (int)comboBox4.SelectedIndex;

            Command_IO.command_string = "stepper " + DEFAULT_PORT + " " + (int)stepper_command + " " + stepper_motor + " " + stepper_angle + " " + "\n";
            
            InfoWindow.AppendText("Stepper command = " + Command_IO.command_string + Environment.NewLine);
            status = Command_IO.do_command(out data);
            InfoWindow.AppendText("Stepper : Return code = " + status + ":: Data = " + data + Environment.NewLine);
        }

        private void do_status(object sender, EventArgs e)
        {
            int data;
            Command_IO.ErrorCode status = Command_IO.ErrorCode.OK;

            Command_IO.command_string = "info " + DEFAULT_PORT + " " + 0 + "\n";

            InfoWindow.AppendText("Info command = " + Command_IO.command_string + Environment.NewLine);
            status = Command_IO.do_command(out data);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            int data, info_cmd;
            Command_IO.ErrorCode status = Command_IO.ErrorCode.OK;

            info_cmd = (int)comboBox6.SelectedIndex;
            Command_IO.command_string = "info " + DEFAULT_PORT + " " + info_cmd + "\n";
            InfoWindow.AppendText("Info command = " + Command_IO.command_string + Environment.NewLine);
            status = Command_IO.do_command(out data);
            InfoWindow.AppendText("Info command reply = " + Command_IO.reply_string + Environment.NewLine);
            if (status == Command_IO.ErrorCode.OK) {
                Nos_servos = Command_IO.int_parameters[2];
                Nos_steppers = Command_IO.int_parameters[2];
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            int data, delay;

            Command_IO.ErrorCode status = Command_IO.ErrorCode.OK;

            delay = (int)numericUpDown2.Value;
            Command_IO.command_string = "delay " + DEFAULT_PORT + " " + delay + "\n";
            InfoWindow.AppendText("Delay cmd = " + Command_IO.command_string + Environment.NewLine);
            status = Command_IO.do_command(out data);
            InfoWindow.AppendText("Delay command reply = " + Command_IO.reply_string + Environment.NewLine);
        }
    }
}

// build_command : Format an ASCII string ready to be sent to the uP
// =============

//private build_command(char cmd_name, int port, int register, int data)
//{
//    Command_IO.command_string = cmd_name + " " + port + " " + register + " " + data + "\n";
//    return;
//}

//***********************************************************
// do_command : execute command on uP/FPGA system
// ==========

//public Command_IO.ErrorCode do_command(out int data)
//{

//    mutex.WaitOne();
//    Command_IO.ErrorCode status = Command_IO.ErrorCode.OK;

//    Command_IO.reply_string = "\n";
//    status = Command_IO.send_command();
//    data = 0;
//    if (status != Command_IO.ErrorCode.OK) {
//        return status;
//    }
//    for (; ; ) {
//        status = Command_IO.get_reply();
//        if ((Command_IO.reply_string[0] == 'D') && (Command_IO.reply_string[1] == ':')) {
//            DebugWindow.AppendText(Command_IO.reply_string + Environment.NewLine);
//            continue;
//        }
//        else {
//            break;
//        }
//    }
//    mutex.ReleaseMutex();
//    return status;
//}

// get_reply : Read a status/data reply from LLcontrol subsystem
//
//public Int32 get_reply()
//{

//    //serialPort1.DiscardInBuffer();
//    serialPort1.ReadTimeout = READ_TIMEOUT;
//    try {
//        Command_IO.reply_string = serialPort1.ReadLine();
//    }
//    catch (TimeoutException) {
//        DebugWindow.AppendText("ReadLine timeout fail" + Environment.NewLine);
//        return -1;
//    }
//    DebugWindow.AppendText("Reply = " + Command_IO.reply_string);
//    return SUCCESS;
//}

