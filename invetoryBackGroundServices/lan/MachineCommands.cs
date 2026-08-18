
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace MATICA_S3300e.LAN
{
    public class MachineCommands
    {
        #region Declarations
        //classes
        private ActionClass _action;
        private MachineConnectionClass _connectioninfo;
        private MachineInfoJSON _machineinfo;
        private CardData _data;
        private Logger _log;
        //private
        private string JsonCommand;
        private int reply;
        //public
        public string sReadMAGData;
        public string sChip;

        #endregion

        #region Properties
        public ActionClass httpAction
        {
            get { return _action; }
            set { _action = value; }
        }
        public MachineConnectionClass ConnectionInfo
        {
            get { return _connectioninfo; }
            set { _connectioninfo = value; }
        }
        public MachineInfoJSON MachineInfo
        {
            get { return _machineinfo; }
            set { _machineinfo = value; }
        }
        public CardData Data
        {
            get { return _data; }
            set { _data = value; }
        }
        #endregion

        #region Constructors
      
        public MachineCommands(ActionClass action, MachineConnectionClass conn, MachineInfoJSON info, CardData data, Logger log)
        {
            _action = action;
            _connectioninfo = conn;
            _machineinfo = info;
            _data = data;
            _log = log;
        }
        #endregion

        #region Enum
        public enum Commands
        {
            GetInfo,
            GetInfoJson,
            Restore,
            Emboss,
            CoverOpen,

            LoadCard,
            EjectCard,
            RetractCard, //??
            RejectCard,

            WriteMAG,
            ReadMAG,

            MoveToChip,
            MoveToCLess,
            ChipReset,
            ApduExchange
        }
        #endregion

        #region Methods ...

        #region CommandManagement
        public int CommandManagement(Commands Command, ref string sMessage)
        {
            reply = 0;
            JsonCommand = string.Empty;
            EjectCardClass EjectCommand;

            try
            {
                switch (Command)
                {
                    case Commands.GetInfo:
                        #region GetInfo

                        JsonCommand = "{\"Command\":\"" + Command.ToString() + "\"}"; //string with command to send
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOST(JsonCommand, Command.ToString(), ref sMessage); // 4 parameters (Machine ip + port, json command (obj serialized), command name, sMessage)

                        if (httpAction.sAction != "echo")
                        {
                            // Matica Print Flow, status-parsing fix: this used to call
                            // `Parser.GetInfoParsing(sMessage, ref _machineinfo)`, commented out
                            // and left dead - no `Parser` class exists anywhere in this codebase
                            // (confirmed by repeated search), so the retry loop that wrapped it
                            // never actually parsed anything and never actually failed either.
                            // Replaced with real structured deserialization into MachineInfoJSON
                            // (see CardDataBean.cs's [JsonProperty] additions) instead of leaving
                            // sMessage as raw text for the caller to string-match against.
                            try
                            {
                                _machineinfo = JsonConvert.DeserializeObject<MachineInfoJSON>(sMessage);
                            }
                            catch (JsonException)
                            {
                                _log.AppendLog("GetInfo parsing failed!", Logger.LogType.Error);
                                return -1;
                            }
                        }

                        #endregion
                        break;
                    case Commands.GetInfoJson:
                        #region GetInfoJson

                        JsonCommand = "{\"Command\":\"" + Command.ToString() + "\"}"; //string with command to send

                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOSTGetInfoJson(JsonCommand, ref sMessage);
                        //reply = httpPOST(JsonCommand, Command.ToString(), ref sMessage);

                        #endregion
                        break;

                    case Commands.Restore:
                        #region Restore

                        JsonCommand = "{\"Command\":\"" + Command.ToString() + "\"}"; //string with command to send
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOST(JsonCommand, Command.ToString(), ref sMessage);

                        #endregion
                        break;

                    case Commands.CoverOpen: //TODO
                        #region CoverOpen

                        //CoverOpenClass CoverOpenCommand = new CoverOpenClass(Command.ToString(), "0");


                        //JsonCommand = new JavaScriptSerializer().Serialize(CoverOpenCommand); //serialize the obj into a string to send
                        //_log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        //reply = httpPOST(JsonCommand, CoverOpenCommand.Command, ref sMessage);

                        CoverOpenClass CoverOpenCommand = new CoverOpenClass(Command.ToString(), "0");

                        JsonCommand = JsonConvert.SerializeObject(
                            CoverOpenCommand,
                            new JsonSerializerSettings
                            {
                                ContractResolver = new DefaultContractResolver(), 
                                Formatting = Formatting.None
                            });

                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOST(JsonCommand, CoverOpenCommand.Command, ref sMessage);


                        #endregion
                        break;

                    case Commands.LoadCard:
                        #region LoadCard

                        LoadCardClass LoadCommand = new LoadCardClass(Command.ToString(), Data.FeederID);

                        //JsonCommand = new JavaScriptSerializer().Serialize(LoadCommand); //serialize the obj into a string to send
                        JsonCommand = JsonConvert.SerializeObject(
                            LoadCommand,
                            new JsonSerializerSettings
                            {
                                ContractResolver = new DefaultContractResolver(),
                                Formatting = Formatting.None
                            });
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOST(JsonCommand, LoadCommand.Command, ref sMessage);

                        #endregion
                        break;

                    case Commands.EjectCard:
                        #region EjectCard

                        EjectCommand = new EjectCardClass(Command.ToString(), Data.StackerID);

                        //JsonCommand = new JavaScriptSerializer().Serialize(EjectCommand); //serialize the obj into a string to send

                        JsonCommand = JsonConvert.SerializeObject(
                         EjectCommand,
                         new JsonSerializerSettings
                         {
                             ContractResolver = new DefaultContractResolver(),
                             Formatting = Formatting.None
                         });
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOST(JsonCommand, EjectCommand.Command, ref sMessage);

                        #endregion
                        break;

                    case Commands.RejectCard:
                        #region RejectCard

                        EjectCommand = new EjectCardClass(Commands.EjectCard.ToString(), "-1");

                        //JsonCommand = new JavaScriptSerializer().Serialize(EjectCommand); //serialize the obj into a string to send
                        JsonCommand = JsonConvert.SerializeObject(
                            EjectCommand,
                            new JsonSerializerSettings
                            {
                                ContractResolver = new DefaultContractResolver(),
                                Formatting = Formatting.None
                            });

                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOST(JsonCommand, EjectCommand.Command, ref sMessage);

                        #endregion
                        break;

                    case Commands.RetractCard: //TODO
                        #region RetractCard

                        JsonCommand = "{\"Command\":\"" + Command.ToString() + "\"}";
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOST(JsonCommand, Command.ToString(), ref sMessage);

                        #endregion
                        break;

                    case Commands.WriteMAG:
                        #region WriteMag

                        WriteMAGClass WriteMAGCommand = new WriteMAGClass(Command.ToString(),
                                                                          Data.Coercivity,
                                                                          Data.Tk1,
                                                                          Data.Tk2,
                                                                          Data.Tk3);

                        //JsonCommand = new JavaScriptSerializer().Serialize(WriteMAGCommand); //serialize the obj into a string to send

                           JsonCommand = JsonConvert.SerializeObject(
                            WriteMAGCommand,
                            new JsonSerializerSettings
                            {
                                ContractResolver = new DefaultContractResolver(),
                                Formatting = Formatting.None
                            });
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOST(JsonCommand, WriteMAGCommand.Command, ref sMessage);

                        #endregion
                        break;

                    case Commands.Emboss:
                        #region Emboss

                        List<EmbossLine> EmbossList = new List<EmbossLine>();

                        for (int nr = 0; nr < CardData.MaxEmbLine; nr++)
                        {
                            if (Data.EmbossLineText[nr].Length > 0)
                                EmbossList.Add(new EmbossLine(Data.EmbossLineFont[nr], Data.EmbossLineCpi[nr], Data.EmbossLineX[nr], Data.EmbossLineY[nr], Data.EmbossLineText[nr]));
                        }

                        EmbossLineClass EmbossCommand = new EmbossLineClass(Command.ToString(), EmbossList, Data.TipperEnable,
                            CLS.Machine_Configuration._TIPTEMP.ToString(), CLS.Machine_Configuration._TIPPRES.ToString(),
                            CLS.Machine_Configuration._TIPCONS.ToString(), CLS.Machine_Configuration._TIPTIME.ToString());

                        //JsonCommand = new JavaScriptSerializer().Serialize(EmbossCommand); //serialize the obj into a string to send
                        JsonCommand = JsonConvert.SerializeObject(
                            EmbossCommand,
                            new JsonSerializerSettings
                            {
                                ContractResolver = new DefaultContractResolver(),
                                Formatting = Formatting.None
                            });
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOST(JsonCommand, EmbossCommand.Command, ref sMessage);

                        #endregion
                        break;

                    case Commands.ReadMAG:
                        #region ReadMag

                        ReadMAGClass ReadMAGCommand = new ReadMAGClass(Command.ToString(), Data.ReadTrackID);

                        //JsonCommand = new JavaScriptSerializer().Serialize(ReadMAGCommand); //serialize the obj into a string to send
                        JsonCommand = JsonConvert.SerializeObject(
                             ReadMAGCommand,
                             new JsonSerializerSettings
                             {
                                 ContractResolver = new DefaultContractResolver(),
                                 Formatting = Formatting.None
                             });
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOST(JsonCommand, ReadMAGCommand.Command, ref sMessage);

                        #endregion
                        break;

                    case Commands.MoveToChip:
                        #region MoveToChip

                        sChip = string.Empty;
                        ApduExchangeClass APDUCommand;

                        #region MoveToChip
                        MoveToChipClass ChipCommand = new MoveToChipClass(Command.ToString(), "1");

                        //JsonCommand = new JavaScriptSerializer().Serialize(ChipCommand); //serialize the obj into a string to send
                        JsonCommand = JsonConvert.SerializeObject(
                                ChipCommand,
                                new JsonSerializerSettings
                                {
                                    ContractResolver = new DefaultContractResolver(),
                                    Formatting = Formatting.None
                                });
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOST(JsonCommand, ChipCommand.Command, ref sMessage);

                        if (ValuateReply(reply)) return -1;

                        sChip = ChipCommand.Command + " >> " + sMessage + Environment.NewLine;
                        #endregion

                        #region ChipReset
                        ChipResetClass ChipResetCommand = new ChipResetClass(Commands.ChipReset.ToString(), "N");

                        //JsonCommand = new JavaScriptSerializer().Serialize(ChipResetCommand);
                        JsonCommand = JsonConvert.SerializeObject(
                            ChipResetCommand,
                            new JsonSerializerSettings
                            {
                                ContractResolver = new DefaultContractResolver(),
                                Formatting = Formatting.None
                            });
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOST(JsonCommand, ChipResetCommand.Command, ref sMessage);

                        if (ValuateReply(reply)) return -1;

                        sChip += ChipResetCommand.Command + " >> " + sMessage + Environment.NewLine;
                        #endregion

                        #region APDU1
                        APDUCommand = new ApduExchangeClass(Commands.ApduExchange.ToString(),
                                                            "00A404000E315041592E5359532E4444463031");

                        //JsonCommand = new JavaScriptSerializer().Serialize(APDUCommand);

                        JsonCommand = JsonConvert.SerializeObject(
                            APDUCommand,
                            new JsonSerializerSettings
                            {
                                ContractResolver = new DefaultContractResolver(),
                                Formatting = Formatting.None
                            });
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        httpPOST(JsonCommand, APDUCommand.Command, ref sMessage);

                        sChip += Environment.NewLine + "APDU1: " + Environment.NewLine;

                        if (httpAction.sAction != "echo")
                        {
                            sChip += "Sent >> " + APDUCommand.APDU + Environment.NewLine;
                            sChip += "Received >> ";
                        }

                        sChip += sMessage + Environment.NewLine;
                        #endregion

                        #region APDU2
                        APDUCommand = new ApduExchangeClass(Commands.ApduExchange.ToString(),
                                                            "00c0000022");

                        //JsonCommand = new JavaScriptSerializer().Serialize(APDUCommand);
                        JsonCommand = JsonConvert.SerializeObject(
                            APDUCommand,
                            new JsonSerializerSettings
                            {
                                ContractResolver = new DefaultContractResolver(),
                                Formatting = Formatting.None
                            });
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        httpPOST(JsonCommand, APDUCommand.Command, ref sMessage);

                        sChip += Environment.NewLine + "APDU2: " + Environment.NewLine;

                        if (httpAction.sAction != "echo")
                        {
                            sChip += "Sent >> " + APDUCommand.APDU + Environment.NewLine;
                            sChip += "Received >> ";
                        }

                        sChip += sMessage + Environment.NewLine;
                        #endregion

                        #endregion
                        break;

                    case Commands.MoveToCLess:
                        #region MoteToCLess

                        sChip = string.Empty;
                        ApduExchangeClass APDUComm;

                        #region MoveToCless
                        MoveToChipClass CLessCommand = new MoveToChipClass(Commands.MoveToChip.ToString(), "2");

                        //JsonCommand = new JavaScriptSerializer().Serialize(CLessCommand); //serialize the obj into a string to send
                        JsonCommand = JsonConvert.SerializeObject(
                        CLessCommand,
                        new JsonSerializerSettings
                        {
                            ContractResolver = new DefaultContractResolver(),
                            Formatting = Formatting.None
                        });
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        reply = httpPOST(JsonCommand, CLessCommand.Command, ref sMessage);

                        if (ValuateReply(reply)) return -1;

                        sChip = CLessCommand.Command + " >> " + sMessage + Environment.NewLine;
                        #endregion

                        #region APDU1
                        APDUComm = new ApduExchangeClass(Commands.ApduExchange.ToString(),
                                                            "00A404000E315041592E5359532E4444463031");

                        //JsonCommand = new JavaScriptSerializer().Serialize(APDUComm);
                        JsonCommand = JsonConvert.SerializeObject(
                            APDUComm,
                            new JsonSerializerSettings
                            {
                                ContractResolver = new DefaultContractResolver(),
                                Formatting = Formatting.None
                            });
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        httpPOST(JsonCommand, APDUComm.Command, ref sMessage);

                        sChip += Environment.NewLine + "APDU1: " + Environment.NewLine;

                        if (httpAction.sAction != "echo")
                        {
                            sChip += "Sent >> " + APDUComm.APDU + Environment.NewLine;
                            sChip += "Received >> ";
                        }

                        sChip += sMessage + Environment.NewLine;
                        #endregion

                        #region APDU2
                        APDUComm = new ApduExchangeClass(Commands.ApduExchange.ToString(),
                                                            "00c0000022");

                        //JsonCommand = new JavaScriptSerializer().Serialize(APDUComm);
                        JsonCommand = JsonConvert.SerializeObject(
                            APDUComm,
                            new JsonSerializerSettings
                            {
                                ContractResolver = new DefaultContractResolver(),
                                Formatting = Formatting.None
                            });
                        _log.AppendLog("Command >> " + JsonCommand, Logger.LogType.Info);

                        httpPOST(JsonCommand, APDUComm.Command, ref sMessage);

                        sChip += Environment.NewLine + "APDU2: " + Environment.NewLine;

                        if (httpAction.sAction != "echo")
                        {
                            sChip += "Sent >> " + APDUComm.APDU + Environment.NewLine;
                            sChip += "Received >> ";
                        }

                        sChip += sMessage + Environment.NewLine;
                        #endregion

                        #endregion
                        break;
                }

                return reply;
            }
            catch (Exception ex)
            {
                sMessage = ex.Message;
                _log.AppendLog(ex.Message, Logger.LogType.Error);
                return -1;
                // if the server returns a 500 error than the webRequest.GetResponse() method
                // throws an exception and all I get is "The remote server returned an error: (500)."
            }
        }
        public int SendCustomCommand(string Command, ref string sMessage)
        {
            try
            {
                int reply = httpPOST(Command, null, ref sMessage);

                return reply;
            }
            catch (Exception ex)
            {
                sMessage = ex.Message;
                return -1;
            }
        }
        #endregion

        #region httpPost
        /// <summary>
        /// function to communicate with raspberry via http POST
        /// </summary>
        /// <param name="JsonCommand"></param>
        /// <param name="CommandName"></param>
        /// <param name="sMessage"></param>
        /// <returns>return '-1' in case of any error or '0' if OK</returns>
        private int httpPOST(string JsonCommand, string CommandName, ref string sMessage)
        {
           
            

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
               | SecurityProtocolType.Tls11
               | SecurityProtocolType.Tls12;


            //define the request (url, content type and method)
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://" + ConnectionInfo.MachineIP() + "/" + httpAction.sAction);
            //var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://172.30.240.155:33200/action");

            //set timeout
            //httpWebRequest.Timeout = 10000; //10 Seconds

            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            //function to accept the certificate
            httpWebRequest.ServerCertificateValidationCallback += ((sender, certificate, chain, sslPolicyErrors) => true);
            //JsonCommand = "{\"Command\":\"LoadCard\",\"Feeder_ID\":\"0\"}";
            string utf8String = string.Empty;

            // Get UTF16 bytes and convert UTF16 bytes to UTF8 bytes
            byte[] utf16Bytes = Encoding.Unicode.GetBytes(JsonCommand);
            byte[] utf8Bytes = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, utf16Bytes);

            // Fill UTF8 bytes inside UTF8 string
            for (int i = 0; i < utf8Bytes.Length; i++)
            {
                // Because char always saves 2 bytes, fill char with 0
                byte[] utf8Container = new byte[2] { utf8Bytes[i], 0 };
                utf8String += BitConverter.ToChar(utf8Container, 0);
            }

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(utf8String); //write the data to the url passed to the .create method
            }

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse(); //wait for response (if it doesn't get it after a while it goes into timeout)

            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                string Answer = streamReader.ReadToEnd(); //contains the response

                AnswerClass JsonAnswer = new AnswerClass();

                // Matica Print Flow, dead-code pass: was Nancy.Json's JavaScriptSerializer, the
                // last real use of the Nancy package in this codebase. JsonConvert is already used
                // for every outbound serialization in this same file; AnswerClass's plain
                // PascalCase properties (Answer/Data/Error) match Newtonsoft's default
                // case-insensitive matching identically, so this is a behavior-preserving swap.
                JsonAnswer = JsonConvert.DeserializeObject<AnswerClass>(Answer);

                _log.AppendLog("Response << " + Answer, Logger.LogType.Info);

                if (httpAction.sAction == "echo")
                {
                    sMessage = Answer;
                    return 0;
                }

                if (JsonAnswer.Answer == "KO") //in case of error "Answer":"KO"
                {
                    sMessage = "Error: " + "Group: " + JsonAnswer.Error.group + ", ErrNumber: " + JsonAnswer.Error.code + " - " + JsonAnswer.Error.message;
                    return -1;
                }

                if (CommandName != null)
                {
                    sMessage = CommandName + " " + JsonAnswer.Answer;

                    if (CommandName == Commands.ReadMAG.ToString())
                        sReadMAGData = JsonAnswer.Data; //save the MAG

                    if (CommandName == Commands.GetInfo.ToString())
                        sMessage = JsonAnswer.Data; //save the data to parsalize

                    if (CommandName == Commands.MoveToChip.ToString() ||
                        CommandName == Commands.ChipReset.ToString() ||
                        CommandName == Commands.ApduExchange.ToString())
                    {
                        if (JsonAnswer.Data == string.Empty)
                        {
                            sMessage = "No ATR";
                            return -1;
                        }
                        else
                            sMessage = JsonAnswer.Data; //save chip data
                    }
                }
                else
                {
                    sMessage = Answer;
                }

                return 0;
            }
        }
        private int httpPOSTGetInfoJson(string JsonCommand, ref string sMessage)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
               | SecurityProtocolType.Tls11
               | SecurityProtocolType.Tls12;

            //define the request (url, content type and method)
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("https://" + ConnectionInfo.ip + ":33201" + "/" + httpAction.sAction);

            //set timeout
            httpWebRequest.Timeout = 10000; //10 Seconds

            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            //function to accept the certificate
            httpWebRequest.ServerCertificateValidationCallback += ((sender, certificate, chain, sslPolicyErrors) => true);

            string utf8String = string.Empty;

            // Get UTF16 bytes and convert UTF16 bytes to UTF8 bytes
            byte[] utf16Bytes = Encoding.Unicode.GetBytes(JsonCommand);
            byte[] utf8Bytes = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, utf16Bytes);

            // Fill UTF8 bytes inside UTF8 string
            for (int i = 0; i < utf8Bytes.Length; i++)
            {
                // Because char always saves 2 bytes, fill char with 0
                byte[] utf8Container = new byte[2] { utf8Bytes[i], 0 };
                utf8String += BitConverter.ToChar(utf8Container, 0);
            }

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(utf8String); //write the data to the url passed to the .create method
            }

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse(); //wait for response (if it doesn't get it after a while it goes into timeout)

            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                sMessage = streamReader.ReadToEnd(); //contains the response

                return 0;
            }
        }
        #endregion

        #region CheckConnection
        public int CheckConnection(ref string errorMessage)
        {
            string ip = ConnectionInfo.ip;
            int port = Convert.ToInt32(ConnectionInfo.port);
            const int timeout = 3000; //3 secondi di timeout sia per il controllo dell'ip che per la porta 

            _log.AppendLog("Check connection..", Logger.LogType.Info);
            //check if IP is pingable
            #region Ping IP

            bool Pingable = false;
            Ping _ping = null;

            try
            {
                _ping = new Ping();
                PingReply reply = _ping.Send(ip, timeout);
                Pingable = reply.Status == IPStatus.Success;
                _log.AppendLog(ip + " Pingable >> " + Pingable.ToString(), Logger.LogType.Info);
            }
            catch (PingException ex)
            {
                errorMessage = ex.ToString();
                // Discard PingExceptions and return -1;
            }
            finally
            {
                if (_ping != null)
                {
                    _ping.Dispose();
                }
            }

            if (!Pingable)
            {
                errorMessage = "Unable to Ping the IP " + ip;
                _log.AppendLog(errorMessage, Logger.LogType.Error);
                return -1;
            }
            #endregion

            //check if port is closed or blocked
            #region Check Port
            try
            {
                using (var client = new TcpClient())
                {
                    var result = client.BeginConnect(ip, port, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(timeout);
                    if (!success)
                    {
                        errorMessage = "Connection error! Port " + Convert.ToString(port) + " closed or blocked.";
                        _log.AppendLog(errorMessage, Logger.LogType.Error);
                        return -1;
                    }

                    _log.AppendLog(ip + ":" + port + " connection >> " + success.ToString(), Logger.LogType.Info);
                    client.EndConnect(result);
                }

            }
            catch
            {
                errorMessage = "Connection error! Port closed or blocked.";
                _log.AppendLog(errorMessage, Logger.LogType.Error);
                return -1;
            }
            #endregion

            return 0;
        }
        #endregion

        #region ValuateReply
        /// <summary>
        /// Valuate Reply 
        /// <br> return true if Reply != 0 </br>
        /// </summary>
        /// <param name="Reply"></param>
        /// <returns></returns>
        public bool ValuateReply(int Reply)
        {
            return (Reply != 0) ? true : false;
        }
        #endregion
        #endregion
    }
}
