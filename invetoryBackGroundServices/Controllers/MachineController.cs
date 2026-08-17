using AUBServicesLayer.Enums;
using AUBServicesLayer.Params;
using CLS;
using INI.AUB.Inventory.System.API.DTO.Card;
using INI.AUB.Inventory.System.API.Enums;
using invetoryBackGroundServices.Params;
using MATICA_S3300e.CLS;
using MATICA_S3300e.LAN;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.IO;

using static System.Net.Mime.MediaTypeNames;
using invetoryBackGroundServices.Helper;

namespace invetoryBackGroundServices.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MachineController : ControllerBase
    {
        private readonly MachineConnectionClass ConnectionInfo;
        private readonly MachineInfoJSON MachineInfo;
        private readonly ActionClass httpAction;
        private readonly CardData Data;
        private readonly API_Handle api_Handle;
        private readonly APIHelper api_Helper;

        string ERROR = string.Empty;
        Logger _log = new Logger(
           Assembly.GetExecutingAssembly().GetName().Name + "_LOG",
           Path.Combine(AppContext.BaseDirectory, "AppLog"),
           true,
           true);
        string InfoText = string.Empty;
        string cardPAN = string.Empty;
        MachineCommands MachineComm;
        public MachineController(MachineConnectionClass connectionInfo, MachineInfoJSON machineInfo, ActionClass HttpAction, CardData data, API_Handle _api_handle,APIHelper _api_helper)
        {
            this.ConnectionInfo = connectionInfo;
            this.MachineInfo = machineInfo;
            this.httpAction = HttpAction;
            this.Data = data;
            api_Handle = _api_handle;
            this.api_Helper = _api_helper;
            MachineComm = new MachineCommands(HttpAction, connectionInfo, machineInfo, data, this._log);
        }
        [HttpPost("get-machine-info")]
        public async Task<IActionResult> GetMachineInfo(GetMachineInfoReques PrintRequest)
        {
            ConnectionInfo.ip = UTILITIES.FormatIP(out ERROR, PrintRequest.Ip);
            ConnectionInfo.port = PrintRequest.Port;
            httpAction.sAction = "action";

            int Reply = MachineComm.CommandManagement(MachineCommands.Commands.GetInfoJson, ref InfoText);
            if (MachineComm.ValuateReply(Reply))
            {
                return BadRequest(new
                {
                    success = false,
                    message = enumType.Error + InfoText
                });


            }
            return Ok(new
            {
                success = true,
                message = InfoText
            });
        }

        [HttpPost("reset-machine")]
        public async Task<IActionResult> ResetMachine(GetMachineInfoReques PrintRequest)
        {
            ConnectionInfo.ip = UTILITIES.FormatIP(out ERROR, PrintRequest.Ip);
            ConnectionInfo.port = PrintRequest.Port;
            httpAction.sAction = "action";
            int Reply = MachineComm.CommandManagement(MachineCommands.Commands.Restore, ref InfoText);
            if (MachineComm.ValuateReply(Reply))
            {
                return BadRequest(new
                {
                    success = false,
                    message = enumType.Error + InfoText
                });


            }
            return Ok(new
            {
                success = true,
                message = InfoText
            });
        }




        [HttpPost("Eject-card")]
        public async Task<IActionResult> EjectCard(EjectCardReq Dto)
        {
            ConnectionInfo.ip = UTILITIES.FormatIP(out ERROR, Dto.Ip);
            ConnectionInfo.port = Dto.Port;
            httpAction.sAction = "action";
            Data.StackerID = Dto.HooperId.ToString();
            int Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
            if (MachineComm.ValuateReply(Reply))
            {
                return BadRequest(new
                {
                    success = false,
                    message = enumType.Error + InfoText
                });


            }

            return Ok(new
            {
                success = true,
                message = InfoText
            });
        }




        //[HttpPost("Print-Card-Holder-Name")]
        //public async Task<IActionResult> Print(PrintParams dto)
        //{
        //    string Error=string.Empty;
        //    string localBatch = string.Empty;
        //    string InfoText = string.Empty;
        //    string logData = string.Empty;
        //    ConnectionInfo.ip = UTILITIES.FormatIP(out ERROR, dto.ip);
        //    ConnectionInfo.port = dto.port;
        //    httpAction.sAction = "action";
        //    Data.FeederID = dto.feederId.ToString();
        //    Data.StackerID = dto.hooperId.ToString();
        //    Data.ReadTrackID = "2";

        //    #region get machine info and check status 
        //    int Reply = MachineComm.CommandManagement(MachineCommands.Commands.GetInfoJson, ref InfoText);
        //    if (MachineComm.ValuateReply(Reply))
        //    {
        //        return BadRequest(new
        //        {
        //            success = false,
        //            message = enumType.Error + InfoText
        //        });


        //    }
        //    if (!(InfoText.Contains("\"machine_status\": \"READY\"") && InfoText.Contains(" \"card_inside\": \"no\"") && InfoText.Contains(" \"tipper_status\": \"Ready\"")))
        //    {
        //        return BadRequest("machine isn't ready to print yet !");
        //    }
        //    #endregion

        //    #region loadcard
        //    Reply = MachineComm.CommandManagement(MachineCommands.Commands.LoadCard, ref InfoText);
        //    if (MachineComm.ValuateReply(Reply))
        //    {
        //        return BadRequest(new
        //        {
        //            success = false,
        //            message = enumType.Error + InfoText
        //        });
        //    }
        //    #endregion

        //    #region ReadMAGData
        //    Reply = MachineComm.CommandManagement(MachineCommands.Commands.ReadMAG, ref InfoText);
        //    if (MachineComm.ValuateReply(Reply))
        //    {
        //        for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;

        //        Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
        //        if (MachineComm.ValuateReply(Reply))
        //        {

        //            return BadRequest(new
        //            {
        //                success = false,
        //                message = "cann't eject card   !"
        //            });

        //        }
        //        return BadRequest(new
        //        {
        //            success = false,
        //            message = enumType.Error + InfoText
        //        });
        //    }
        //    cardPAN = MachineComm.sReadMAGData.Substring(0, 16);
        //    cardPAN = "**********" + cardPAN.Substring(10, 6);
        //    #endregion

        //    #region Check Card Exist 
        //    if (!await api_Handle.GetChaeckCardExist(cardPAN, dto.product.id, dto.branch.BranchName,dto.token))
        //    {
        //        for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;
        //        logData = " [Card Holder Name:" + dto.cardHolderName + "] [Card Type:" + dto.product.productName +
        //             "] [Card PAN:" + cardPAN + " Card Not Found] [Status:Error]";
        //        _log.AppendLog(logData, Logger.LogType.Error);

        //        Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
        //        if (MachineComm.ValuateReply(Reply))
        //        {

        //            return BadRequest(new
        //            {
        //                success = false,
        //                message = enumType.Error + InfoText
        //            });

        //        }
        //        // ================================================= Clear Data
        //        return BadRequest(new
        //        {
        //            success = false,
        //            message = "card not found !"
        //        });


        //    }
        //    #endregion

        //    #region Load Embossing information 
        //    for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;
        //    Data.EmbossLineText[0] = dto.cardHolderName.Trim();
        //    Data.EmbossLineFont[0] = dto.printConfiguration.Font.ToString();
        //    Data.EmbossLineCpi[0] = dto.printConfiguration.Cpi.ToString();
        //    Data.EmbossLineX[0] = dto.printConfiguration.OffSetX.ToString();
        //    Data.EmbossLineY[0] = dto.printConfiguration.OffSetY.ToString();
        //    Data.TipperEnable = "Y";
        //    #endregion

        //    #region EmbossCardHolderName
        //    Reply = MachineComm.CommandManagement(MachineCommands.Commands.Emboss, ref InfoText);
        //    if (MachineComm.ValuateReply(Reply))
        //    {
        //         localBatch = Path.Combine(AppContext.BaseDirectory, "LocalBatch.lbt");

        //        if (!System.IO.File.Exists(localBatch)) System.IO.File.Create(localBatch).Close();
        //        using (StreamWriter streamWriter = new StreamWriter(localBatch, true))
        //        {
        //            string lineValue = $"{cardPAN}|" +
        //                $"{dto.cardHolderName.Trim()}|" +
        //                $"{dto.branch.Id}|" +
        //                $"{dto.branch.BranchName}|" +
        //                $"{dto.userName}|3|Error in Print Card|" +
        //                $"{Convert.ToInt32(dto.product.id)}";
        //            string lineValueCipher = ENCRYPTION.Enc_TripleDES(out Error, lineValue, GLOBALS._KEY_CONFIG);
        //            streamWriter.WriteLine(lineValueCipher);
        //            streamWriter.Close();
        //        }
        //        logData = " [Card Holder Name:" + dto.cardHolderName + "] [Card Type:" + dto.product.productName +
        //           "] [Card PAN:" + cardPAN + " Card Not Found] [Status:Error]";
        //        _log.AppendLog(logData, Logger.LogType.Error);
        //        bool isuploaded = await API_Handle.SetPrintLogAsync(
        //                cardPAN,
        //                dto.cardHolderName.Trim(),
        //                Convert.ToInt32(dto.branch.Id),
        //                dto.branch.BranchName,
        //                dto.userName,
        //                3,
        //                "Error in Print Card",
        //               dto.product.id,dto.token
        //                );
        //        if (!isuploaded)
        //        {
        //            return BadRequest(new
        //            {
        //                success = false,
        //                message = "cann't Update card status the card is failed to print !"
        //            });
        //        }
        //        for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;


        //        Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
        //        if (MachineComm.ValuateReply(Reply))
        //        {

        //            return BadRequest(new
        //            {
        //                success = false,
        //                message = enumType.Error + InfoText
        //            });

        //        }
        //        // ================================================= Clear Data



        //        return BadRequest(new
        //        {
        //            success = false,
        //            message = "Error Printing Card"
        //        });

        //    }
        //    #endregion

        //    #region save to local batch
        //    logData = " [Card Holder Name:" + dto.cardHolderName + "] [Card Type:" + dto.product.productName +
        //           "] [Card PAN:" + cardPAN + " Print Card Success] [Status:Success]";
        //    _log.AppendLog(logData, Logger.LogType.Error);
        //    localBatch = Path.Combine(AppContext.BaseDirectory, "LocalBatch.lbt");

        //    if (!System.IO.File.Exists(localBatch)) System.IO.File.Create(localBatch).Close();
        //    using (StreamWriter streamWriter = new StreamWriter(localBatch, true))
        //    {
        //        string lineValue = $"{cardPAN}|" +
        //            $"{dto.cardHolderName.Trim()}|" +
        //            $"{dto.branch.Id}|" +
        //            $"{dto.branch.BranchName}|" +
        //            $"{dto.userName}|1|Print Card Success|" +
        //            $"{Convert.ToInt32(dto.product.id)}";
        //        string lineValueCipher = ENCRYPTION.Enc_TripleDES(out ERROR, lineValue, GLOBALS._KEY_CONFIG);
        //        streamWriter.WriteLine(lineValueCipher);
        //        streamWriter.Close();
        //        #endregion
        //    }
        //    #region EjectCard
        //        bool isUploaded = await API_Handle.SetPrintLogAsync(
        //                 cardPAN,
        //                dto.cardHolderName.Trim(),
        //                dto.branch.Id,
        //               dto.branch.BranchName,
        //                dto.userName,
        //                 1,
        //                 "Print Card Success",
        //                 dto.product.id
        //                 ,dto.token
        //                 );
        //        if (!isUploaded)
        //        {
        //        Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
        //        if (MachineComm.ValuateReply(Reply))
        //        {

        //            return BadRequest(new
        //            {
        //                success = false,
        //                message = "card printed failed to eject card !"
        //            });

        //        }
        //        return BadRequest(new
        //            {
        //                success = false,
        //                message = "card printed failed to save please eject card ! !"
        //            });
        //        }
        //        for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;

        //        Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
        //        if (MachineComm.ValuateReply(Reply))
        //        {

        //            return BadRequest(new
        //            {
        //                success = false,
        //                message = "card printed failed to eject card !"
        //            });

        //        }
        //        return Ok(new
        //        {
        //            success = false,
        //            message = "card printed "
        //        });
        //        #endregion
















        //}

        [HttpPost("Print-Card-Holder-Name")]
        public async Task<IActionResult> Print(PrintReqDto dto)
        {
            MachineConfig? machineConfig;
            PrintConfiguration? printConfig;
            ProductVM? product;
            Branch? branch;
            try
            {
                PrintConfigForSpecificFaceReq PrintConfigReq= new PrintConfigForSpecificFaceReq
                {
                    productName= dto.productName,
                    printedFace=dto.printedFace,
                };
                 machineConfig = await api_Helper.CallGetApiAsync<MachineConfig,string>(dto.token, "MachineConfigrations/machines/details",dto.machineIp);
                if (machineConfig == null) { return BadRequest(new { message = "cann't get machine config please check you server connections !" }); }
                 printConfig =await api_Helper.CallGetApiAsync<PrintConfiguration, PrintConfigForSpecificFaceReq>(dto.token, "PrintConfigurations/get-print-config-for-sepecific-face", PrintConfigReq);
                if (printConfig == null) return BadRequest(new { message = "cann't get print config please check you server connections !" });
                product = await api_Helper.CallGetApiAsync<ProductVM, string>(dto.token, "Products/get-product-by-name", $"{dto.productName}", treatStringAsQuery: true);
                if (product == null) return BadRequest(new { message = "cann't get product config please check you server connections !" });
                branch = await api_Helper.CallGetApiAsync<Branch, string>(dto.token,"Branch/branches", dto.branchName);
                if (branch == null) return BadRequest(new { message = "cann't get branch data please check you server connections !" });

            }
            catch (Exception ex) 
            {
                return BadRequest(new { message = "cann't load any config please check server connection !" });
            }
            string Error = string.Empty;
            string localBatch = string.Empty;
            string InfoText = string.Empty;
            string logData = string.Empty;
            ConnectionInfo.ip = UTILITIES.FormatIP(out ERROR,dto.machineIp);
            ConnectionInfo.port = machineConfig.port;
            httpAction.sAction = "action";
            Data.FeederID = machineConfig.feederId.ToString();
            Data.StackerID = machineConfig.hooperId.ToString();
            Data.ReadTrackID = "2";

            #region get machine info and check status 
            int Reply = MachineComm.CommandManagement(MachineCommands.Commands.GetInfoJson, ref InfoText);
            if (MachineComm.ValuateReply(Reply))
            {
                return BadRequest(new
                {
                    success = false,
                    message = enumType.Error + InfoText
                });


            }
            if (!(InfoText.Contains("\"machine_status\": \"READY\"") && InfoText.Contains(" \"card_inside\": \"no\"") && InfoText.Contains(" \"tipper_status\": \"Ready\"")))
            {
                return BadRequest("machine isn't ready to print yet !");
            }
            #endregion

            #region loadcard
            Reply = MachineComm.CommandManagement(MachineCommands.Commands.LoadCard, ref InfoText);
            if (MachineComm.ValuateReply(Reply))
            {
                return BadRequest(new
                {
                    success = false,
                    message = enumType.Error + InfoText
                });
            }
            #endregion

            #region ReadMAGData
            Reply = MachineComm.CommandManagement(MachineCommands.Commands.ReadMAG, ref InfoText);
            if (MachineComm.ValuateReply(Reply))
            {
                for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;

                Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
                if (MachineComm.ValuateReply(Reply))
                {

                    return BadRequest(new
                    {
                        success = false,
                        message = "cann't eject card   !"
                    });

                }
                return BadRequest(new
                {
                    success = false,
                    message = enumType.Error + InfoText
                });
            }
            cardPAN = MachineComm.sReadMAGData.Substring(0, 16);
            cardPAN = "**********" + cardPAN.Substring(10, 6);
            #endregion

            #region Check Card Exist 
            if (!await api_Handle.GetChaeckCardExist(cardPAN, product.id, dto.branchName, dto.token))
            {
                for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;
                logData = " [Card Holder Name:" + dto.cardHolderName + "] [Card Type:" + product.productName +
                     "] [Card PAN:" + cardPAN + " Card Not Found] [Status:Error]";
                _log.AppendLog(logData, Logger.LogType.Error);

                Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
                if (MachineComm.ValuateReply(Reply))
                {

                    return BadRequest(new
                    {
                        success = false,
                        message = "card not found !"
                    });

                }
                // ================================================= Clear Data
                return BadRequest(new
                {
                    success = false,
                    message = "card not found !"
                });


            }
            #endregion

            #region Load Embossing information 
            for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;
            Data.EmbossLineText[0] = dto.cardHolderName.Trim();
            Data.EmbossLineFont[0] = printConfig.font.ToString();
            Data.EmbossLineCpi[0] =printConfig.cpi.ToString();
            Data.EmbossLineX[0] = printConfig.offSetX.ToString();
            Data.EmbossLineY[0] =printConfig.offSetY.ToString();
            Data.TipperEnable = "Y";
            #endregion

            #region EmbossCardHolderName
            Reply = MachineComm.CommandManagement(MachineCommands.Commands.Emboss, ref InfoText);
            if (MachineComm.ValuateReply(Reply))
            {
                localBatch = Path.Combine(AppContext.BaseDirectory, "LocalBatch.lbt");

                if (!System.IO.File.Exists(localBatch)) System.IO.File.Create(localBatch).Close();
                using (StreamWriter streamWriter = new StreamWriter(localBatch, true))
                {
                    string lineValue = $"{cardPAN}|" +
                        $"{dto.cardHolderName.Trim()}|" +
                        $"{branch.id}|" +
                        $"{dto.branchName}|" +
                        $"{dto.userName}|3|Error in Print Card|" +
                        $"{Convert.ToInt32(product.id)}";
                    string lineValueCipher = ENCRYPTION.Enc_TripleDES(out Error, lineValue, GLOBALS._KEY_CONFIG);
                    streamWriter.WriteLine(lineValueCipher);
                    streamWriter.Close();
                }
                logData = " [Card Holder Name:" + dto.cardHolderName + "] [Card Type:" + product.productName +
                   "] [Card PAN:" + cardPAN + " Card Not Found] [Status:Error]";
                _log.AppendLog(logData, Logger.LogType.Error);
                bool isuploaded = await API_Handle.SetPrintLogAsync(
                        cardPAN,
                        dto.cardHolderName.Trim(),
                        Convert.ToInt32(branch.id),
                        branch.branchName,
                        dto.userName,
                        3,
                        "Error in Print Card",
                       product.id, dto.token
                        );
                if (!isuploaded)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "cann't Update card status the card is failed to print !"
                    });
                }
                for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;


                Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
                if (MachineComm.ValuateReply(Reply))
                {

                    return BadRequest(new
                    {
                        success = false,
                        message = enumType.Error + InfoText
                    });

                }
                // ================================================= Clear Data



                return BadRequest(new
                {
                    success = false,
                    message = "Error Printing Card"
                });

            }
            #endregion

            #region save to local batch
            logData = " [Card Holder Name:" + dto.cardHolderName + "] [Card Type:" + product.productName +
                   "] [Card PAN:" + cardPAN + " Print Card Success] [Status:Success]";
            _log.AppendLog(logData, Logger.LogType.Info);
            localBatch = Path.Combine(AppContext.BaseDirectory, "LocalBatch.lbt");

            if (!System.IO.File.Exists(localBatch)) System.IO.File.Create(localBatch).Close();
            using (StreamWriter streamWriter = new StreamWriter(localBatch, true))
            {
                string lineValue = $"{cardPAN}|" +
                    $"{dto.cardHolderName.Trim()}|" +
                    $"{branch.id}|" +
                    $"{branch.branchName}|" +
                    $"{dto.userName}|1|Print Card Success|" +
                    $"{Convert.ToInt32(product.id)}";
                string lineValueCipher = ENCRYPTION.Enc_TripleDES(out ERROR, lineValue, GLOBALS._KEY_CONFIG);
                streamWriter.WriteLine(lineValueCipher);
                streamWriter.Close();
                #endregion
            }
            #region EjectCard
            bool isUploaded = await API_Handle.SetPrintLogAsync(
                     cardPAN,
                    dto.cardHolderName.Trim(),
                    branch.id,
                   branch.branchName,
                    dto.userName,
                     1,
                     "Print Card Success",
                     product.id
                     , dto.token
                     );
            if (!isUploaded)
            {
                Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
                if (MachineComm.ValuateReply(Reply))
                {

                    return BadRequest(new
                    {
                        success = false,
                        message = "card printed failed to eject card !"
                    });

                }
                return BadRequest(new
                {
                    success = false,
                    message = "card printed failed to save please eject card ! !"
                });
            }
            for (int i = 0; i < 20; i++) Data.EmbossLineText[i] = string.Empty;

            Reply = MachineComm.CommandManagement(MachineCommands.Commands.EjectCard, ref InfoText);
            if (MachineComm.ValuateReply(Reply))
            {

                return BadRequest(new
                {
                    success = false,
                    message = "card printed failed to eject card !"
                });

            }
            return Ok(new
            {
                success = false,
                message = "card printed "
            });
            #endregion
















        }
    }
}
