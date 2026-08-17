using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MATICA_S3300e.LAN
{
    // =============================== Card Data
    public class CardData
    {
        #region Declarations ...
        private static int _maxEmbLine = 20;

        private int _cardstoproduce;
        private string _processmag;
        private string _processemb;

        private string _trackToRead;
        private string _feederid;
        private string _stackerid;

        private string _coercivity;
        private string _tk1;
        private string _tk2;
        private string _tk3;

        private string _readtrackid;

        private string _tipperenable;

        private string[] _embosslinefont = new string[MaxEmbLine];
        private string[] _embosslinecpi = new string[MaxEmbLine];
        private string[] _embosslinex = new string[MaxEmbLine];
        private string[] _embossliney = new string[MaxEmbLine];
        private string[] _embosslinetext = new string[MaxEmbLine];
        #endregion

        #region Properties
        public int CardsToProduce
        {
            get { return _cardstoproduce; }
            set { _cardstoproduce = value; }
        }
        public static int MaxEmbLine
        {
            get { return _maxEmbLine; }
            set { _maxEmbLine = value; }
        }
        public string ProcessMag
        {
            get { return _processmag; }
            set { _processmag = value; }
        }
        public string ProcessEmb
        {
            get { return _processemb; }
            set { _processemb = value; }
        }
        public string FeederID
        {
            get { return _feederid; }
            set { _feederid = value; }
        }
        public string TrackToRead
        {
            get { return _trackToRead; }
            set { _trackToRead = value; }
        }
        public string StackerID
        {
            get { return _stackerid; }
            set { _stackerid = value; }
        }
        public string Coercivity
        {
            get { return _coercivity; }
            set { _coercivity = value; }
        }
        public string Tk1
        {
            get { return _tk1; }
            set { _tk1 = value; }
        }
        public string Tk2
        {
            get { return _tk2; }
            set { _tk2 = value; }
        }
        public string Tk3
        {
            get { return _tk3; }
            set { _tk3 = value; }
        }
        public string ReadTrackID
        {
            get { return _readtrackid; }
            set { _readtrackid = value; }
        }
        public string TipperEnable
        {
            get { return _tipperenable; }
            set { _tipperenable = value; }
        }
        public string[] EmbossLineFont
        {
            get { return _embosslinefont; }
            set { _embosslinefont = value; }
        }
        public string[] EmbossLineCpi
        {
            get { return _embosslinecpi; }
            set { _embosslinecpi = value; }
        }
        public string[] EmbossLineX
        {
            get { return _embosslinex; }
            set { _embosslinex = value; }
        }
        public string[] EmbossLineY
        {
            get { return _embossliney; }
            set { _embossliney = value; }
        }
        public string[] EmbossLineText
        {
            get { return _embosslinetext; }
            set { _embosslinetext = value; }
        }
        #endregion

        #region Constructors
        public CardData()
        {
            ClearCardData();
        }
        #endregion

        #region Methods
        public void addEmbossBufferLine(int Index, string EmbFont, string EmbCpi, string EmbX, string EmbY, string EmbText)
        {
            if (Index < MaxEmbLine)
            {
                _embosslinefont[Index] = EmbFont;
                _embosslinecpi[Index] = EmbCpi;
                _embosslinex[Index] = EmbX;
                _embossliney[Index] = EmbY;
                _embosslinetext[Index] = EmbText;
            }
        }

        /// <summary>
        /// to Clear the data structure
        /// </summary>
        public void ClearCardData()
        {
            _processmag = string.Empty;
            _processemb = string.Empty;
            _feederid = string.Empty;
            _stackerid = string.Empty;

            _trackToRead = string.Empty;
            _coercivity = string.Empty;
            _tk1 = string.Empty;
            _tk2 = string.Empty;
            _tk3 = string.Empty;

            for (int i = 0; i < MaxEmbLine; i++)
            {
                _embosslinefont[i] = string.Empty;
                _embosslinecpi[i] = string.Empty;
                _embosslinex[i] = string.Empty;
                _embossliney[i] = string.Empty;
                _embosslinetext[i] = string.Empty;
            }
        }

        /// <summary>
        /// To Print the data structure
        /// </summary>
        /// <returns></returns>
        public string CardDataText()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("CARD DATA :");

            sb.AppendLine("Process MAG = " + _processmag + ", EMB = " + _processemb);
            sb.AppendLine("Load from Feeder = " + _feederid + " and Eject to Stacker = " + _stackerid);
            sb.AppendLine("Track to Read = " + _trackToRead);

            string sCoercivity;
            if (_coercivity == "1")
                sCoercivity = "MAG LOCO";
            else if (_coercivity == "2")
                sCoercivity = "MAG HICO";
            else
                sCoercivity = "MAG Coercitivity Not Set";

            sb.AppendLine(sCoercivity);
            sb.AppendLine("TK1 = " + _tk1);
            sb.AppendLine("TK2 = " + _tk2);
            sb.AppendLine("TK3 = " + _tk3);

            sb.AppendLine("MaxEmbLine = " + _maxEmbLine);
            for (int i = 0; i < MaxEmbLine; i++)
            {
                if (_embosslinex[i] == string.Empty) break;

                sb.Append("EmbLine" + (i + 1) + " = ");
                sb.Append("F" + _embosslinefont[i] + " ");
                sb.Append("CS" + _embosslinecpi[i] + " ");
                sb.Append("X" + _embosslinex[i] + " ");
                sb.Append("Y" + _embossliney[i] + " ");
                sb.AppendLine(" " + _embosslinetext[i]);
            }

            return sb.ToString();
        }

        #endregion
    }
    public class MachineInfoJSON
    {
        #region Declarations
        private string _machinestatus;
        private string _errorvalue;
        private string _errorinfo;
        private string _cardinside;
        private string _coveropen;
        private List<string> _feeder;
        private int _cardcounter;
        private string _tipperstatus;
        private int _tippertemperature;
        private string _tippernearend;
        private string _frontinfillernearend;
        private string _rearinfillernearend;
        private string _machinesn;
        private string _embosserversion;
        #endregion

        #region Properties
        public string MachineStatus
        {
            get { return _machinestatus; }
            set { _machinestatus = value; }
        }

        public string ErrorValue
        {
            get { return _errorvalue; }
            set { _errorvalue = value; }
        }
        public string ErrorInfo
        {
            get { return _errorinfo; }
            set { _errorinfo = value; }
        }
        public string CardInside
        {
            get { return _cardinside; }
            set { _cardinside = value; }
        }
        public string CoverOpen
        {
            get { return _coveropen; }
            set { _coveropen = value; }
        }
        public List<string> Feeder
        {
            get { return _feeder; }
            set { _feeder = value; }
        }
        public int CardCounter
        {
            get { return _cardcounter; }
            set { _cardcounter = value; }
        }
        public string TipperStatus
        {
            get { return _tipperstatus; }
            set { _tipperstatus = value; }
        }
        public int TipperTemperature
        {
            get { return _tippertemperature; }
            set { _tippertemperature = value; }
        }
        public string TipperNearEnd
        {
            get { return _tippernearend; }
            set { _tippernearend = value; }
        }
        public string FrontInFillerNearEnd
        {
            get { return _frontinfillernearend; }
            set { _frontinfillernearend = value; }
        }
        public string RearInFillerNearEnd
        {
            get { return _rearinfillernearend; }
            set { _rearinfillernearend = value; }
        }
        public string MachineSN
        {
            get { return _machinesn; }
            set { _machinesn = value; }
        }
        public string EmbosserVersion
        {
            get { return _embosserversion; }
            set { _embosserversion = value; }
        }
        #endregion

        #region Constructors
        public MachineInfoJSON()
        {
            _machinestatus = string.Empty;

            _errorinfo = string.Empty;
            _errorvalue = string.Empty;

            _cardinside = string.Empty;
            _coveropen = string.Empty;

            if (_feeder != null)
                _feeder.Clear();
            else
                _feeder = new List<string>();

            _cardcounter = 0;

            _tipperstatus = string.Empty;
            _tippertemperature = 0;

            _tippernearend = string.Empty;
            _frontinfillernearend = string.Empty;
            _rearinfillernearend = string.Empty;

            _machinesn = string.Empty;
            _embosserversion = string.Empty;
        }
        #endregion

        #region Methods
        /// <summary>
        /// To Print the data structure
        /// </summary>
        /// <returns></returns>
        public string MachineInfoTextJSON()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("MACHINE INFO :");

            sb.AppendLine("Machine Status: " + _machinestatus);
            sb.AppendLine("Card Inside: " + _cardinside);
            sb.AppendLine("Cover Open: " + _coveropen);

            if (Feeder != null)
                for (int i = 0; i < _feeder.Count(); i++)
                    sb.AppendLine("Feeder " + i + ": " + _feeder[i]);

            sb.AppendLine("Card Counter: " + _cardcounter);
            sb.AppendLine("Tipper Status: " + _tipperstatus + ", Tipper Temperature: " + TipperTemperature);
            sb.AppendLine("Tipper NearEnd: " + _tippernearend);
            sb.AppendLine("FrontInfiller NearEnd: " + _frontinfillernearend);
            sb.AppendLine("RearInfiller NearEnd: " + _rearinfillernearend);

            sb.AppendLine("Machine SN: " + _machinesn);
            sb.AppendLine("Embosser Version: " + _embosserversion);

            return sb.ToString();
        }
        #endregion
    }
    public class MachineConnectionClass
    {
        #region Declarations
        private string _ip;
        private string _port;
        #endregion

        #region Properties
        public string ip
        {
            get { return _ip; }
            set { _ip = value; }
        }
        public string port
        {
            get { return _port; }
            set { _port = value; }
        }
        #endregion

        #region Constructors
        public MachineConnectionClass()
        {
            //_ip = "192.168.70.70:33200";
            _ip = "10.20.50.233";
            _port = "33200";
        }
        #endregion

        #region Methods
        public string MachineIP()
        {
            if (_ip != string.Empty && _port != string.Empty)
                return _ip + ":" + _port;
            else
                return "10.20.50.233";

        }
        #endregion
    }
    public class ActionClass
    {
        #region Declarations
        private string _action;
        private bool _check;
        #endregion

        #region Properties
        public string sAction
        {
            get { return _action; }
            set { _action = value; }
        }
        public bool ActionCheckError
        {
            get { return _check; }
            set { _check = value; }
        }
        #endregion

        #region Constructors
        public ActionClass()
        {
            _action = "action";
            _check = false;
        }
        #endregion

        #region Methods
        public void ActionCheck()
        {
            _check = false;

            if (_action != "action" && _action != "echo")
            {
                _action = "action";
                _check = true;
            }
        }
        #endregion
    }
    // =============================== Command Classes
    public class LoadCardClass
    {
        #region Declarations
        private string _Command;
        private string _feeder_id;
        #endregion

        #region Properties
        public string Command
        {
            get { return _Command; }
            set { _Command = value; }
        }

        public string Feeder_ID
        {
            get { return _feeder_id; }
            set { _feeder_id = value; }
        }
        #endregion

        #region Constructors
        public LoadCardClass(string command, string feeder_id)
        {
            _Command = command;
            _feeder_id = feeder_id;
        }
        #endregion
    }
    public class EjectCardClass
    {
        #region Declarations
        private string _command;
        private string _reject_id;
        #endregion

        #region Properties
        public string Command
        {
            get { return _command; }
            set { _command = value; }
        }

        public string Reject_ID
        {
            get { return _reject_id; }
            set { _reject_id = value; }
        }
        #endregion

        #region Constructors
        public EjectCardClass(string command, string reject_id)
        {
            _command = command;
            _reject_id = reject_id;
        }
        #endregion
    }

    public class MagData
    {
        #region Declarations
        private string _coercivity;
        private string _tk1;
        private string _tk2;
        private string _tk3;
        #endregion

        #region Properties
        public string coercivity
        {
            get { return _coercivity; }
            set { _coercivity = value; }
        }
        public string tk1
        {
            get { return _tk1; }
            set { _tk1 = value; }
        }
        public string tk2
        {
            get { return _tk2; }
            set { _tk2 = value; }
        }
        public string tk3
        {
            get { return _tk3; }
            set { _tk3 = value; }
        }
        #endregion

        #region Constructors
        public MagData(string coercivity, string tk1, string tk2, string tk3)
        {
            _coercivity = coercivity;
            _tk1 = tk1;
            _tk2 = tk2;
            _tk3 = tk3;
        }
        #endregion
    }
    public class WriteMAGClass
    {
        #region Declarations
        private string _command;
        private MagData _magdata;
        #endregion

        #region Properties
        public string Command
        {
            get { return _command; }
            set { _command = value; }
        }

        public MagData MagData
        {
            get { return _magdata; }
            set { _magdata = value; }
        }
        #endregion

        #region Constructors
        public WriteMAGClass(string command, string coercivity, string tk1, string tk2, string tk3)
        {
            _command = command;
            _magdata = new MagData(coercivity, tk1, tk2, tk3);
        }
        #endregion
    }
    public class ReadMAGClass
    {
        #region Declarations
        private string _command;
        private string _track_id;
        #endregion

        #region Properties
        public string Command
        {
            get { return _command; }
            set { _command = value; }
        }
        public string Track_ID
        {
            get { return _track_id; }
            set { _track_id = value; }
        }
        #endregion

        #region Constructors
        public ReadMAGClass(string command, string track_id)
        {
            _command = command;
            _track_id = track_id;
        }
        #endregion
    }

    public class EmbossLine
    {
        #region Declarations
        private string _font;
        private string _cpi;
        private string _x;
        private string _y;
        private string _text;
        #endregion

        #region Properties
        public string font
        {
            get { return _font; }
            set { _font = value; }
        }
        public string cpi
        {
            get { return _cpi; }
            set { _cpi = value; }
        }
        public string x
        {
            get { return _x; }
            set { _x = value; }
        }
        public string y
        {
            get { return _y; }
            set { _y = value; }
        }
        public string text
        {
            get { return _text; }
            set { _text = value; }
        }
        #endregion

        #region Constructors
        public EmbossLine(string font, string cpi, string x, string y, string text)
        {
            _font = font;
            _cpi = cpi;
            _x = x;
            _y = y;
            _text = text;
        }
        #endregion
    }
    public class EmbossLineClass
    {
        #region Declarations
        private string _command;
        private List<EmbossLine> _emboss_line;
        private string _tipper_on;
        private string _tipper_termperature;
        private string _tipper_pressure;
        private string _tipper_consuming;
        private string _tipper_time;
        #endregion

        #region Properties
        public string Command
        {
            get { return _command; }
            set { _command = value; }
        }
        public List<EmbossLine> Emboss_Line
        {
            get { return _emboss_line; }
            set { _emboss_line = value; }
        }
        public string Tipper_ON
        {
            get { return _tipper_on; }
            set { _tipper_on = value; }
        }
        public string Tipper_termperature
        {
            get { return _tipper_termperature; }
            set { _tipper_termperature = value; }
        }
        public string Tipper_pressure
        {
            get { return _tipper_pressure; }
            set { _tipper_pressure = value; }
        }
        public string Tipper_consuming
        {
            get { return _tipper_consuming; }
            set { _tipper_consuming = value; }
        }
        public string Tipper_time
        {
            get { return _tipper_time; }
            set { _tipper_time = value; }
        }
        #endregion

        #region Constructors
        public EmbossLineClass(string command, List<EmbossLine> emboss_line, string tipper_on,string tipTemp,string tipPress,string tipCons,string tipTime)
        {
            _command = command;
            _emboss_line = emboss_line;
            _tipper_on = tipper_on;
            _tipper_pressure = tipPress;
            _tipper_termperature = tipTemp;
            _tipper_consuming = tipCons;
            _tipper_time = tipTime;
        }
        #endregion
    }

    public class CoverOpenClass
    {
        #region Declarations
        private string _command;
        private string _parameter;
        #endregion

        #region Properties
        public string Command
        {
            get { return _command; }
            set { _command = value; }
        }
        public string Parameter
        {
            get { return _parameter; }
            set { _parameter = value; }
        }
        #endregion

        #region Constructors
        public CoverOpenClass(string command, string parameter)
        {
            Command = command;
            Parameter = parameter;
        }
        #endregion
    }
    public class MoveToChipClass
    {
        #region Declarations
        private string _command;
        private string _chip_station;
        #endregion

        #region Properties
        public string Command
        {
            get { return _command; }
            set { _command = value; }
        }
        public string Chip_Station
        {
            get { return _chip_station; }
            set { _chip_station = value; }
        }
        #endregion

        #region Constructors
        public MoveToChipClass(string command, string chip_station)
        {
            _command = command;
            _chip_station = chip_station;
        }
        #endregion
    }
    public class ChipResetClass
    {
        #region Declarations
        private string _command;
        private string _unpower;
        #endregion

        #region Properties
        public string Command
        {
            get { return _command; }
            set { _command = value; }
        }
        public string Unpower
        {
            get { return _unpower; }
            set { _unpower = value; }
        }
        #endregion

        #region Constructors
        public ChipResetClass(string command, string unpower)
        {
            _command = command;
            _unpower = unpower;
        }
        #endregion
    }

    public class ApduExchangeClass
    {
        #region Declarations
        private string _command;
        private string _apdu;
        #endregion

        #region Properties
        public string Command
        {
            get { return _command; }
            set { _command = value; }
        }
        public string APDU
        {
            get { return _apdu; }
            set { _apdu = value; }
        }
        #endregion

        #region Constructors
        public ApduExchangeClass(string command, string apdu)
        {
            _command = command;
            _apdu = apdu;
        }
        #endregion
    }
    public class AnswerClass
    {
        #region Declarations
        private string _answer;
        private Error _error;
        private string _data;
        #endregion

        #region Properties
        public string Answer
        {
            get { return _answer; }
            set { _answer = value; }
        }
        public Error Error
        {
            get { return _error; }
            set { _error = value; }
        }
        public string Data
        {
            get { return _data; }
            set { _data = value; }
        }
        #endregion

        #region Constructors
        public AnswerClass()
        {
            _answer = string.Empty;
            _error = new Error();
            _data = string.Empty;
        }

        public AnswerClass(string answer, string data, string group, string code, string message)
        {
            _answer = answer;
            _error = new Error(group, code, message);
            _data = data;
        }
        #endregion
    }
    public class Error
    {
        #region Declarations
        private string _group;
        private string _code;
        private string _message;
        #endregion

        #region Properties
        public string group
        {
            get { return _group; }
            set { _group = value; }
        }
        public string code
        {
            get { return _code; }
            set { _code = value; }
        }
        public string message
        {
            get { return _message; }
            set { _message = value; }
        }
        #endregion

        #region Constructors
        public Error()
        {
            _group = string.Empty;
            _code = string.Empty;
            _message = string.Empty;
        }

        public Error(string group, string code, string message)
        {
            _group = group;
            _code = code;
            _message = message;
        }
        #endregion
    }
}
