using System;

using Pi_the_Robot_test;

using System.IO.Ports;
using System.Windows.Forms;
using static Pi_the_Robot_test.Form1;

namespace Pi_the_Robot_test {
    public class Command_IO {

        //***********************************************************************
        // Globals
        //***********************************************************************

        //***********************************************************************
        // Constant definitions
        //***********************************************************************

        const int COMBAUD = 115200;  // default baud rate
        const int READ_TIMEOUT = 100000;   // timeout for read reply (10 seconds)

        const int MAX_COMMAND_STRING_LENGTH = 100;
        const int MAX_REPLY_STRING_LENGTH = 100;
        const int MAX_COMMAND_PARAMETERS = 10;

        
        //***********************************************************************
        // 
        public struct arg
        {
            public Modes    arg_mode;
            public int      int_arg;
            public float    float_arg;
        }
        int     argc;
        public arg[] argv = new arg[MAX_COMMAND_PARAMETERS];
        public string[] string_parameters = new string[MAX_COMMAND_PARAMETERS];

        public string command_string;
        public string reply_string;


        //***********************************************************************
        // Variables - LOCAL
        //***********************************************************************

        public Int32[] int_parameters = new Int32[MAX_COMMAND_PARAMETERS];
        public float[] float_parameters = new float[MAX_COMMAND_PARAMETERS];
        // public string[] string_parameters = new string[MAX_COMMAND_PARAMETERS];
        private Modes[] param_type = new Modes[MAX_COMMAND_PARAMETERS];

        public enum Modes { MODE_U, MODE_I, MODE_R, MODE_S };

        public enum ErrorCode {
            OK                              = 0,        // uP errors
            LETTER_ERROR                    = -100,
            DOT_ERROR                       = -101,
            PLUSMINUS_ERROR                 = -102,
            BAD_COMMAND                     = -103,
            BAD_PORT_NUMBER                 = -104,
            BAD_NOS_PARAMETERS              = -105,
            BAD_BASE_PARAMETER              = -106,
            PARAMETER_OUTWITH_LIMITS        = -107,
            BAD_SERVO_COMMAND               = -108,
            STEPPER_CALIBRATE_FAIL          = -109,
            BAD_STEPPER_COMMAND             = -110,
            BAD_STEP_VALUE                  = -111,
            MOVE_ON_UNCALIBRATED_MOTOR      = -112,
            EXISTING_FAULT_WITH_MOTOR       = -113,
            SM_MOVE_TOO_SMALL               = -114,
            LIMIT_SWITCH_ERROR              = -115,
            UNKNOWN_STEPPER_MOTOR_STATE     = -116,
            STEPPER_BUSY                    = -117,
            SERVO_BUSY                      = -118,

            BAD_COMPORT_OPEN                = -200,     // PC errors
            UNKNOWN_COM_PORT                = -201,
            BAD_COMPORT_READ                = -202,
            BAD_COMPORT_WRITE               = -203,
            NULL_EMPTY_STRING               = -204,
            BAD_COMPORT_CLOSE               = -205,
        }

        public enum ServoCommands
        {
            ABS_MOVE, 
            ABS_MOVE_SYNC, 
            SPEED_MOVE, 
            SPEED_MOVE_SYNC, 
            RUN_SYNC_MOVES, 
            STOP, 
            STOP_ALL 
        }

        public enum StepperCommands {
            REL_MOVE,
            ABS_MOVE,
            REL_MOVE_SYNC,
            ABS_MOVE_SYNC,
            CALIBRATE,
        }

        //***********************************************************************
        // Objects
        //***********************************************************************

        static SerialPort _serialPort;

        //*********************************************************************
        // constructor
        //*********************************************************************
        public Command_IO()
        {

            _serialPort = new SerialPort();    // Create a new SerialPort object
        }

        //***********************************************************************
        // Methods
        //***********************************************************************
        // open_comms : Initialise specified serial COM port
        // ==========

        public ErrorCode open_comms(string COM_port, int baud)
        {
            ErrorCode status;

            status = ErrorCode.OK;
            _serialPort.BaudRate = baud;
            if (string.IsNullOrEmpty(COM_port)) {
                return ErrorCode.UNKNOWN_COM_PORT;
            }
            _serialPort.PortName = COM_port;

            try {
                _serialPort.Open();
            }
            catch {
                status = ErrorCode.BAD_COMPORT_OPEN;
            }
            _serialPort.NewLine = "\n";
            return status;
        }

        //***********************************************************************
        // Close_comms : Initialise specified serial COM port
        // ===========

        public ErrorCode Close_comms(string COM_port)
        {
            ErrorCode status;

            status = ErrorCode.OK;
            try {
                _serialPort.Close();
            }
            catch {
                status = ErrorCode.BAD_COMPORT_CLOSE;
            }
            return status;
        }

        

        //***********************************************************************
        // do_command : execute remote command and get reply
        // ==========
        //
        // Parameters
        //          command   IN   ASCII string with '\n' terminator
        //          data      OUT  First piece of data returned by executed command
        // Returned value
        //          status         Error value of type 'ErrorCode@

        public ErrorCode do_command(out int data)
        {
            ErrorCode status = ErrorCode.OK;

            status = send_command();

            data = 0;
            if (status != ErrorCode.OK) {
                return status;
            }
            for (; ; ) {
                status = get_reply();
                if ((reply_string[0] == 'D') && (reply_string[1] == ':')) {
                    continue;
                }
                else {
                    break;
                }
            }
            if (status != ErrorCode.OK) {
                return status;
            }
            status = parse_parameter_string(reply_string);
            if (status != ErrorCode.OK) {
                return status;
            }
            data = int_parameters[2];
            if ((ErrorCode)int_parameters[1] != ErrorCode.OK) {
                return (ErrorCode)int_parameters[1];
            }
            return ErrorCode.OK;
        }

        //*********************************************************************** 
        // send_command : Send command string to RP2040 subsystem
        // ============
        public Command_IO.ErrorCode send_command()
        {
            ErrorCode status = ErrorCode.OK;

            try {
                _serialPort.Write(command_string);
            }
            catch {
                status = ErrorCode.BAD_COMPORT_WRITE;
            }
            return status;
        }

        //*********************************************************************** 
        // get_reply : Read a status/data reply string from rp2040 subsystem
        // =========
        public ErrorCode get_reply()
        {

            ErrorCode status = ErrorCode.OK;

            //serialPort1.DiscardInBuffer();
            _serialPort.ReadTimeout = READ_TIMEOUT;
            try {
                reply_string = _serialPort.ReadLine();
            }
            catch (TimeoutException) {
                status = ErrorCode.BAD_COMPORT_READ;
            }
            return status;
        }

        //***************************************************************************
        // parse_parameter_string : analyse string and convert into ints/floats/strings
        // ======================
        //
        // Breaks the command string into a set of token strings that are 
        // labelled REAL, INTEGER or STRING.  
        //

        public ErrorCode parse_parameter_string(string string_data)
        {
            ErrorCode status;
            Int32 index;

            status = ErrorCode.OK;

            //
            // check string

            if (string.IsNullOrEmpty(string_data)) {
                return ErrorCode.NULL_EMPTY_STRING;
            }
            //
            //clear parameter data
        
            for (index = 0; index < MAX_COMMAND_PARAMETERS; index++) {
                int_parameters[index] = 0;
                argv[index].int_arg = 0;
                float_parameters[index] = 0.0F;
                argv[index].float_arg = 0.0F;
                ;
                param_type[index] = Modes.MODE_U;
                argv[index].arg_mode = Modes.MODE_U;
            }
            //
            // split string into individual strings based on SPACE separation

          string_parameters = string_data.Split(new string[] { " ", "\r", "\n" }, MAX_COMMAND_PARAMETERS, StringSplitOptions.RemoveEmptyEntries);
            argc = string_parameters.Length;
            //
            // check each string for INTEGER or REAL values (default is STRING)

            for (index = 0; index < argc; index++) {
                if (Int32.TryParse(string_parameters[index], out int_parameters[index]) == true) {
                    param_type[index] = Modes.MODE_I;
                    argv[index].arg_mode = Modes.MODE_I;
                    continue;
                }
                if (float.TryParse(string_parameters[index], out float_parameters[index]) == true) {
                    param_type[index] = Modes.MODE_R;
                    argv[index].arg_mode = Modes.MODE_R;
                    continue;
                }
                param_type[index] = Modes.MODE_S;
                argv[index].arg_mode = Modes.MODE_S;
            }
            return status;
        }

        //*********************************************************************** 
        // ping : Check 
        // ====
        public ErrorCode Ping(int value, out int data)
        {
            command_string = "ping 0 " + value + "\n";
            return (do_command(out data));
        }



    }
}

//*********************************************************************** 
// get_sys_data : Read register 0 - holds data on number of I/O units
// ============
//public ErrorCode get_sys_data()
//{
//    ErrorCode status;
//    int data;

//    status = (do_command("r 5 0 0\n", out data));
//    if (status != ErrorCode.OK) {
//        return status;
//    }
//    //
//    // update "unit" values

//    // data = int_parameters[1];
//    nos_PWM_units = ((data >> 8) & 0x0F);
//    nos_QE_units = ((data >> 12) & 0x0F);
//    nos_RC_units = ((data >> 16) & 0x0F);
//    //
//    // update pointers to first register of each type of unit

//    SYS_base = 0;
//    PWM_base = SYS_base + SYS_REGISTERS;
//    QE_base = PWM_base + (nos_PWM_units * REGISTERS_PER_PWM_CHANNEL);
//    RC_base = QE_base + (nos_QE_units * REGISTERS_PER_QE_CHANNEL);

//    return ErrorCode.OK;
//}

//***********************************************************************
// execute_command : format and execute a command to uP/FPGA
// ===============
//
// Parameters
//    cmd_name  char  IN   single ASCII character representing uP/FPGA command
//    port      int   IN   return address of command
//    register  int   IN   register address (0->255) if FPGA command
//    in_data   int   IN   data for command  
//    out_data  int   OUT  First piece of data returned by executed command
//
// Returned values
//          status         Error value of type 'ErrorCode@
//          out_data       int returned from FPGA 
//public ErrorCode execute_command(char cmd_name, int port, int register, int in_data, out int out_data)
//{

//    command_string = cmd_name + " " + port + " " + register + " " + in_data + "\n";
//    ErrorCode status = (do_command(out out_data));
//    return status;
//}
