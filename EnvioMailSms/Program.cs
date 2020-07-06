using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EnvioMailSms.Modelos;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Net.Mime;
using System.Xml.Linq;
using System.Xml;

namespace EnvioMailSms
{
    internal static class Program
    {
        private static string Encryptar(string texto, string llave)
        {
            try
            {
                MD5CryptoServiceProvider objHashMD5 = new MD5CryptoServiceProvider();
                byte[] byteHash, byteBuff;
                string strTempKey = llave;
                byteHash = objHashMD5.ComputeHash(ASCIIEncoding.ASCII.GetBytes(strTempKey));
                AesCryptoServiceProvider objDESCrypto =
                    new AesCryptoServiceProvider()
                    {
                        Key = byteHash,
                        Mode = CipherMode.ECB,
                    };
                byteBuff = ASCIIEncoding.ASCII.GetBytes(texto);
                return Convert.ToBase64String(objDESCrypto.CreateEncryptor().
                    TransformFinalBlock(byteBuff, 0, byteBuff.Length));
            }
            catch (Exception ex)
            {
                return "Error al encriptar. " + ex.Message;
            }
        }

        [STAThread]
        private static void Main(string[] args)
        {

            SAPDataContext sapdb = new SAPDataContext();
            ElectroDataContext electrodb = new ElectroDataContext();
            SeguridadDataContext Segurdb = new SeguridadDataContext();
            EximagenCacheDataContext Cachedb = new EximagenCacheDataContext();

            List<int> camposExistentes = Cachedb.ValidaTiendaMensaje.Where(x => x.envio).Select(x => x.docnum).ToList();

            List<OINV> validaTienda = sapdb.OINV.Where(x => x.U_IdTienda != null && x.DocNum != null).ToList();

            validaTienda = validaTienda.Where(x => !camposExistentes.Contains((int)x.DocNum)).ToList();

            if (validaTienda.Count > 0)
            {
                try
                {
                    List<CRD1> listEmail = sapdb.CRD1.ToList();
                    listEmail = listEmail.Where(x => validaTienda.Exists(y => y.CardCode == x.CardCode && y.ShipToCode == x.Address) && x.U_Correo != null).ToList();
                    foreach (CRD1 mailFactura in listEmail)
                    {
                        OINV datafactura = validaTienda.FirstOrDefault(x => x.CardCode == mailFactura.CardCode && x.ShipToCode == mailFactura.Address);
                        int tienda = Convert.ToInt32(datafactura.U_IdTienda);
                        CambioVista datosTienda = Cachedb.CambioVista.Where(x => x.ID == tienda).FirstOrDefault();

                        string userMail = Cachedb.EstilosPorVista.Where(x => x.IDCliente == datosTienda.ID && x.Campo == "UserMail").Select(x => x.Valor).FirstOrDefault();
                        string hostMail = Cachedb.EstilosPorVista.Where(x => x.IDCliente == datosTienda.ID && x.Campo == "HostMail").Select(x => x.Valor).FirstOrDefault();
                        string portMail = Cachedb.EstilosPorVista.Where(x => x.IDCliente == datosTienda.ID && x.Campo == "PortMail").Select(x => x.Valor).FirstOrDefault();
                        string sslMail = Cachedb.EstilosPorVista.Where(x => x.IDCliente == datosTienda.ID && x.Campo == "SslMail").Select(x => x.Valor).FirstOrDefault();
                        string passMail = Cachedb.EstilosPorVista.Where(x => x.IDCliente == datosTienda.ID && x.Campo == "PassMail").Select(x => x.Valor).FirstOrDefault();
                        string urlLogoHeader = Cachedb.EstilosPorVista.Where(x => x.IDCliente == datosTienda.ID && x.Campo == "urlLogoHeader").Select(x => x.Valor).FirstOrDefault();
                        string requested = Cachedb.EstilosPorVista.Where(x => x.IDCliente == datosTienda.ID && x.Campo == "FullUrl").Select(x => x.Valor).FirstOrDefault();

                        SmtpClient smtpClient = new SmtpClient();
                        MailMessage message = new MailMessage();
                        MailAddress fromEmail = new MailAddress(userMail);

                        smtpClient.Host = hostMail;
                        smtpClient.Port = Int32.Parse(portMail);
                        smtpClient.UseDefaultCredentials = false;
                        smtpClient.EnableSsl = (sslMail == "1");
                        smtpClient.Credentials = new System.Net.NetworkCredential(userMail, passMail);
                        message.From = fromEmail;
                        message.To.Add(mailFactura.U_Correo);

                        message.Subject = "Alerta: Nueva  Compra " + datosTienda.Cliente + " – " + mailFactura.U_Contacto + " - " + String.Format("{0:d}", datafactura.DocDate) + " por " + String.Format("{0:C}", datafactura.DocTotal);
                        message.IsBodyHtml = true;

                        string nameJpg = Cachedb.EstilosPorVista.Where(x => x.IDCliente == datosTienda.ID && x.Campo == "urlLogoHeader").Select(x => x.Valor).FirstOrDefault();
                        nameJpg = nameJpg.Substring(1);

                        TSHAK.Components.SecureQueryString QueryString = new TSHAK.Components.SecureQueryString(new Byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4, 5, 8 });
                        QueryString["DocEntry"] = datafactura.DocEntry.ToString();
                        QueryString["DocStatus"] = datafactura.DocStatus.ToString();
                        QueryString["TipoDoc"] = "FACTURA";
                        QueryString["Year"] = datafactura.DocDate.Value.Year.ToString();
                       
                        string urlLogo = Encryptar(requested + urlLogoHeader.Replace("~", ""), "UriLg45");
                        int Numfact = Convert.ToInt32(datafactura.U_FactRel);
                        string Encriptado = Encryptar(Numfact.ToString(), "IdeT23");

                        var xmlBody = electrodb.TBL_XML_MANHATTAN.Where(x => x.FolioWeb == Numfact && x.IdTienda == tienda).Select(x => x.XMLData).FirstOrDefault();
                        
                        string url = "http://www2.promoshop.com.mx/webapi/Facturacion/ExportReport?IDe=" + Encriptado + "&URLLogoE=" + urlLogo;
                       

                        string body = "<HTML>" +
                                        "<BODY >" +
                                        "<table width=\"600\"><tr><td><img src=\"https://www.idealpromo.com.mx" + nameJpg + "\"  height=\"95\" alt=\"Promoline\" /></td></tr></table>" +
                                        "<P STYLE='margin-bottom: 0cm; widows: 2; orphans: 2'><FONT COLOR='#4a4a4a'><FONT FACE='Roboto, sans-serif'><FONT SIZE=5 STYLE='font-size: 19pt'><SPAN STYLE='font-style: normal'><B>Hola&nbsp;" + mailFactura.U_Contacto + "</B></SPAN></FONT></FONT></FONT>" +
                                        "</P>" +
                                        "<P STYLE='margin-bottom: 0cm; widows: 2; orphans: 2'><BR>" +
                                        "</P>" +
                                        "<TABLE CELLPADDING=2 CELLSPACING=2>" +
                                        "	<TR>" +
                                        "		<TD>" +
                                        "			<P STYLE='margin-top: 0.16cm; border: none; padding: 0cm'><FONT FACE='Roboto, sans-serif'><FONT SIZE=4 STYLE='font-size: 15pt'>GRACIAS" +
                                        "			POR TU&nbsp;COMPRA</FONT></FONT></P>" +
                                        "		</TD>" +
                                        "	</TR>" +
                                        "	<TR>" +
                                        "		<TD>" +
                                        "			<P STYLE='margin-top: 0.26cm; border: none; padding: 0cm'><FONT FACE='Roboto, sans-serif'><FONT SIZE=2 STYLE='font-size: 10pt'>Recuerda" +
                                        "			conservar tu n&uacute;mero de pedido para dar seguimiento.</FONT></FONT></P>" +
                                        "		</TD>" +
                                        "	</TR>" +
                                        "</TABLE>" +
                                        "<P STYLE='margin-bottom: 0cm; widows: 2; orphans: 2'><FONT COLOR='#222222'><FONT FACE='Roboto, sans-serif'><FONT SIZE=2 STYLE='font-size: 10pt'><SPAN STYLE='font-style: normal'><B>Detalle" +
                                        "de Cargos</B></SPAN></FONT></FONT></FONT> " +
                                        "</P>" +
                                        "<P STYLE='margin-bottom: 0cm; widows: 2; orphans: 2'><FONT COLOR='#222222'><FONT FACE='Roboto, sans-serif'><FONT SIZE=2 STYLE='font-size: 9pt'><SPAN STYLE='font-style: normal'><SPAN STYLE='font-weight: normal'>No." +
                                        "de pedido:&nbsp;</SPAN></SPAN></FONT></FONT></FONT><FONT COLOR='#222222'><FONT FACE='Roboto, sans-serif'><FONT SIZE=2 STYLE='font-size: 9pt'><SPAN STYLE='font-style: normal'>" + datafactura.DocNum + "</SPAN></FONT></FONT></FONT>" +
                                        "</P>" +
                                        "<P STYLE='margin-bottom: 0cm; widows: 2; orphans: 2'><FONT COLOR='#222222'><FONT FACE='Roboto, sans-serif'><FONT SIZE=2 STYLE='font-size: 9pt'><SPAN STYLE='font-style: normal'><SPAN STYLE='font-weight: normal'>Fecha" +
                                        "estimada de entrega:&nbsp;&nbsp;</SPAN></SPAN></FONT></FONT></FONT><FONT FACE='Roboto, sans-serif'><FONT SIZE=2 STYLE='font-size: 9pt'><SPAN STYLE='font-style: normal'><SPAN STYLE='font-weight: normal'>" + ((DateTime)datafactura.DocDate).ToString("MM/dd/yyyy") +
                                        "</SPAN></SPAN></FONT></FONT> " +
                                        "</P>" +
                                        "<P STYLE='margin-bottom: 0cm; widows: 2; orphans: 2'><FONT COLOR='#222222'><FONT FACE='Roboto, sans-serif'><FONT SIZE=2 STYLE='font-size: 9pt'><SPAN STYLE='font-style: normal'><B>Tarjeta" +
                                        "Bancaria a la que autoriza la compra:&nbsp;</B></SPAN></FONT></FONT></FONT><FONT COLOR='#222222'><FONT FACE='Roboto, sans-serif'><FONT SIZE=2 STYLE='font-size: 9pt'><SPAN STYLE='font-style: normal'>XXXX-XXXX-XXXX-" + datafactura.U_NumCtaPago + "</SPAN></FONT></FONT></FONT>" +
                                        "</P>" +
                                        "<P STYLE='margin-bottom: 0cm; widows: 2; orphans: 2'><BR>" +
                                        "</P>" +
                                        "<P STYLE='margin-bottom: 0cm; widows: 2; orphans: 2'><BR>" +
                                        "</P>" +
                                        "<TABLE CELLPADDING=0 CELLSPACING=0>" +
                                        "	<TR>" +
                                        "		<TD>" +
                                        "			<P STYLE='border: none; padding: 0cm'><FONT COLOR='#000000'><FONT FACE='Roboto, sans-serif'><FONT SIZE=3 STYLE='font-size: 11pt'><B>Direcci&oacute;n" +
                                        "			de Entrega</B></FONT></FONT></FONT></P>" +
                                        "		</TD>" +
                                        "	</TR>" +
                                        "	<TR>" +
                                        "		<TD></TD>" +
                                        "	</TR>" +
                                        "	<TR>" +
                                        "		<TD>" +
                                        "			<P STYLE='border: none; padding: 0cm'><FONT COLOR='#000000'><FONT FACE='Roboto, sans-serif'><FONT SIZE=2 STYLE='font-size: 10pt'>" + datafactura.Address2 +
                                        "			</FONT></FONT></FONT></P>" +
                                        "		</TD>" +
                                        "	</TR>" +
                                        "</TABLE>" +
                                        "<P STYLE='margin-bottom: 0cm; widows: 2; orphans: 2'><BR>" +
                                        "</P>" +
                                        "<P STYLE='margin-bottom: 0cm; widows: 2; orphans: 2'><BR>" +
                                        "</P>" +
                                        "<P STYLE='margin-bottom: 0cm; widows: 2; orphans: 2'><BR>" +
                                        "</P>" +
                                        "</BODY>" +
                                        "</HTML>";
                        string plainBody = "Hola ¡" + mailFactura.U_Correo + "! GRACIAS POR TU COMPRA Recuerda conservar tu número de pedido para dar seguimiento:" + datafactura.FolioNum;

                        AlternateView plainView = AlternateView.CreateAlternateViewFromString(plainBody, null, "text/plain");
                        AlternateView htmlView = AlternateView.CreateAlternateViewFromString(body, null, "text/html");

                        message.AlternateViews.Add(plainView);
                        message.AlternateViews.Add(htmlView);

                        if (xmlBody!=null && String.IsNullOrEmpty(xmlBody.Value))
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                XDocument doc = new XDocument(xmlBody);
                                doc.Save(ms);
                                ms.Position = 0;
                                ContentType contentType = new ContentType();
                                contentType.MediaType = MediaTypeNames.Text.Xml;
                                contentType.Name = "CFDI.xml";

                                Attachment attachment = new Attachment(ms, contentType);
                                message.Attachments.Add(attachment);

                                var request = (HttpWebRequest)WebRequest.Create(url);

                                request.Method = "POST";
                                request.ContentLength = 0;
                                request.ContentType = "application/pdf";

                                using (HttpWebResponse webResponse = (HttpWebResponse)request.GetResponse())
                                {
                                    
                                    using (Stream reader = webResponse.GetResponseStream())
                                    {
                                        using (MemoryStream memStream = new MemoryStream())
                                        {
                                            reader.CopyTo(memStream);
                                            memStream.Position = 0;
                                            ContentType contentTypePDF = new ContentType();
                                            contentTypePDF.MediaType = "application/pdf";
                                            contentTypePDF.Name = "Factura Electronica.pdf";

                                            Attachment attachmentPdf = new Attachment(memStream, contentTypePDF);
                                            message.Attachments.Add(attachmentPdf);
                                            smtpClient.Send(message);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            smtpClient.Send(message);
                        }

                        ValidaTiendaMensaje messageEnviado = new ValidaTiendaMensaje
                        {
                            docnum = (int)datafactura.DocNum,
                            envio = true
                        };
                        Cachedb.ValidaTiendaMensaje.InsertOnSubmit(messageEnviado);
                        Cachedb.SubmitChanges();

                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.ReadKey();
                }
            }
            var fact = from a in sapdb.Notificaciones_Fact
                        select a;

            string[] excludemail = (from a in electrodb.TBL_NOTIFICACION_EXCLUIR
                                    where a.Mail == true
                                    select a.CardCode).ToArray();

            foreach (Notificaciones_Fact singlefact in fact)
            {

                string emailcliente = "";
                string emaila = "";
                string emailb = "";
                string emailc = "";
                string mCC = "";
                List<string> listaClienteExterno;

                try
                {
                    var emailelectro = from a in electrodb.TBL_NOTIFICACION_DATOS
                                        where a.CardCode == singlefact.Cardcode
                                        select a;

                    if (emailelectro!=null && emailelectro.Any())
                    {
                        emaila = emailelectro.First().Mail;
                    }

                    var emaileshop = from a in Segurdb.QACG_CLIENTE_EXTERNO
                                        where a.INO_CLIENTE == singlefact.Cardcode && a.B_Active && !a.BloqEmailFactura
                                        select a;

                    

                    var listemaileshop = (from a in Segurdb.QACG_CLIENTE_EXTERNO
                                            where a.INO_CLIENTE == singlefact.Cardcode && a.B_Active && !a.BloqEmailFactura
                                            select a.SEMAIL).ToList();

                    if (validaTienda.Exists(x => x.DocNum == singlefact.Docnum))
                    {
                        string tienda = validaTienda.Where(x => x.DocNum == singlefact.Docnum).Select(x => x.U_IdTienda).FirstOrDefault();
                        int IdTienda = Convert.ToInt32(tienda);
                        listemaileshop = (from a in Segurdb.QACG_CLIENTE_EXTERNO
                                          where a.B_Active && a.IdTienda == IdTienda && a.IsAdmin && !a.BloqEmailFactura
                                          select a.SEMAIL).ToList();
                    }

                    var distinct = listemaileshop.Distinct();

                    if (emaileshop!= null && emaileshop.Any())
                    {
                        try
                        {
                            var paso = emaileshop.FirstOrDefault();
                            emailb = paso.SEMAIL;
                        }
                        catch(Exception ex)
                        {

                        }

                    }

                    listaClienteExterno = distinct.Where(x => x != emailb).ToList();

                    var emailsap = from a in sapdb.OCRD
                                    where a.CardCode == singlefact.Cardcode
                                    select a;

                    if (emailsap!=null && emailsap.Any())
                    {
                        emailc = emailsap.First().E_Mail;
                    }

                    if (emaila != "")
                    {
                        if (emaila == emailb)
                        {
                            emailb = "";
                        }
                        if (emaila == emailc)
                        {
                            emailc = "";
                        }
                        emailcliente = emaila;
                    }
                    if (emailb != "")
                    {
                        if (emailb == emailc)
                        {
                            emailc = "";
                        }
                        if (emailcliente == "")
                        {
                            emailcliente = emailb;
                        }
                        else
                        {
                            emailcliente = emailcliente + ", " + emailb;
                        }

                    }
                    if (emailc != "")
                    {
                        if (emailcliente == "")
                        {
                            emailcliente = emailc;
                        }
                        else
                        {
                            emailcliente = emailcliente + ", " + emailc;
                        }
                    }


                    var cv = sapdb.Clientes_Vendedor.Where(x => x.CardCode == singlefact.Cardcode).FirstOrDefault();


                    var vend = (from ven in sapdb.OSLP
                                where ven.SlpName == cv.SlpName.ToString()
                                select ven).FirstOrDefault();

                    if (vend != null)
                    {
                        mCC = vend.Memo.ToString();
                    }

                }
                catch (Exception EX)
                {
                    Console.WriteLine(EX.Message);
                    continue;
                }

                int enviadomail = 0;

                TBL_NOTIFICACION_FACT linea = new TBL_NOTIFICACION_FACT();
                if (!excludemail.Contains(singlefact.Cardcode) && emailcliente != "")
                {

                    try
                    {

                        string cc = string.Empty;
                        StringBuilder sendto = new StringBuilder(emailcliente);
                        foreach (string item in listaClienteExterno)
                        {
                            sendto.Append(", " + item);
                        }


                        if (string.IsNullOrEmpty(mCC))
                        {
                            cc = ConfigurationManager.AppSettings["ccMail"];

                        }
                        else
                        {
                            string c = ConfigurationManager.AppSettings["ccMail"];
                            if(!string.IsNullOrEmpty(c))
                                cc = c + "; " + mCC;
                            else
                                cc = mCC;
                        }

                        SmtpClient smtpClient = new SmtpClient();
                        MailMessage message = new MailMessage();
                        MailAddress fromEmail = new MailAddress(ConfigurationManager.AppSettings["userMail"]);

                        smtpClient.Host = ConfigurationManager.AppSettings["hostMail"];
                        smtpClient.Port = Int32.Parse(ConfigurationManager.AppSettings["portMail"]);
                        smtpClient.UseDefaultCredentials = false;
                        smtpClient.EnableSsl = Boolean.Parse(ConfigurationManager.AppSettings["sslMail"]);
                        smtpClient.Credentials = new System.Net.NetworkCredential(ConfigurationManager.AppSettings["userMail"], ConfigurationManager.AppSettings["passMail"]);
                        message.From = fromEmail;
                        message.To.Add(sendto.ToString());

                        foreach (string c in cc.Split(';'))
                            message.Bcc.Add(c);

                        message.Subject = "Alerta: Nueva  Factura Eximagen-Promoline – " + singlefact.Docnum + " - " + String.Format("{0:d}", singlefact.DocDate) + " por " + String.Format("{0:C}", singlefact.Doctotal);
                        message.IsBodyHtml = true;

                        LinkedResource lr = new LinkedResource(AppDomain.CurrentDomain.BaseDirectory + "Resources\\notificacion_factura.jpg", System.Net.Mime.MediaTypeNames.Image.Jpeg);
                        lr.ContentId = "imagepromo";
                        lr.TransferEncoding = System.Net.Mime.TransferEncoding.Base64;

                        TSHAK.Components.SecureQueryString QueryString = new TSHAK.Components.SecureQueryString(new Byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4, 5, 8 });
                        QueryString["DocEntry"] = singlefact.DocEntry.ToString();
                        QueryString["DocStatus"] = singlefact.DocStatus.ToString();
                        QueryString["TipoDoc"] = "FACTURA";
                        QueryString["Year"] = singlefact.DocDate.Value.Year.ToString();

                        string body = "";

                        string plainBody = "";


                        byte[] encbuff = Encoding.UTF8.GetBytes(singlefact.Docnum.ToString());
                        string numfact = HttpServerUtility.UrlTokenEncode(encbuff);

                        string url = "http://www2.promoshop.com.mx/Factura_Electro/Default.aspx?data=" + HttpUtility.UrlEncode(QueryString.ToString());
                        string url2 = "http://www2.promoshop.com.mx/Factura_Electro/Notificacion.aspx?data=" + numfact;

                        if (camposExistentes.Contains((int)singlefact.Docnum))
                        {
                            body = "<html><head></head><style type=\"text/css\">body { font-family:Verdana, Geneva, sans-serif; }</style><body><table width=\"600\"><tr><td><img src=cid:imagepromo width=\"596\" height=\"95\" alt=\"Promoline\" /><p>Estimado Cliente.  Se realizó una compra desde su portal de ventas.</p><p>Encuentra en este correo el detalle de la factura que acabamos de emitirte.</p><table><tr><td align=\"right\" width=\"170\">Numero de Cliente :</td><td><b>" + singlefact.Cardcode + "</b></td></tr><tr><td align=\"right\">Nombre:</td><td><b> " + singlefact.cardname + "</b></td></tr><tr><td align=\"right\">Numero de Factura:</td><td> <b>" + singlefact.Docnum + "</b></td></tr><tr><td align=\"right\">Fecha:</td><td> <b>" + String.Format("{0:d}", singlefact.DocDate) + "</b></td></tr><tr><td align=\"right\">Importe:</td><td> <b>" + String.Format("{0:C}", singlefact.Doctotal) + "</b></td></tr></table><br />Haz click <a href=\"" + url + "\" target='_blank'><b>aqui</b></a> si deseas descargar tu factura.<p>Si por algún motivo tu NO reconoces esta factura como una solicitud tuya, por favor da Click en <a href=\"" + url2 + "\" >esta liga</a> para notificarnos y actuar de inmediato.</p><p>Si deseas editar la lista de correos electrónicos o celulares que recibirán estas notificaciones por favor ingresa al portal E-Commerce (https://www.promoline.com.mx) en la sección de Mi Cuenta / Descargar Mis Facturas.  Aquí encontraras una opción para que elijas tu configuración de alertas.</p></td></tr></table></body></html>";
                            plainBody = "Estimado Cliente. Se realizó una compra desde su portal de ventas. Encuentra su factura en el siguiente enlace:" + url;
                        }
                        else
                        {
                            body = "<html><head></head><style type=\"text/css\">body { font-family:Verdana, Geneva, sans-serif; }</style><body><table width=\"600\"><tr><td><img src=cid:imagepromo width=\"596\" height=\"95\" alt=\"Promoline\" /><p>Estimado Cliente.  Agradecemos tu compra.</p><p>Encuentra en este correo el detalle de la factura que acabamos de emitirte.</p><table><tr><td align=\"right\" width=\"170\">Numero de Cliente :</td><td><b>" + singlefact.Cardcode + "</b></td></tr><tr><td align=\"right\">Nombre:</td><td><b> " + singlefact.cardname + "</b></td></tr><tr><td align=\"right\">Numero de Factura:</td><td> <b>" + singlefact.Docnum + "</b></td></tr><tr><td align=\"right\">Fecha:</td><td> <b>" + String.Format("{0:d}", singlefact.DocDate) + "</b></td></tr><tr><td align=\"right\">Importe:</td><td> <b>" + String.Format("{0:C}", singlefact.Doctotal) + "</b></td></tr></table><br />Haz click <a href=\"" + url + "\" target='_blank'><b>aqui</b></a> si deseas descargar tu factura.<p>Si por algún motivo tu NO reconoces esta factura como una solicitud tuya, por favor da Click en <a href=\"" + url2 + "\" >esta liga</a> para notificarnos y actuar de inmediato.</p><p>Si deseas editar la lista de correos electrónicos o celulares que recibirán estas notificaciones por favor ingresa al portal E-Commerce (https://www.promoline.com.mx) en la sección de Mi Cuenta / Descargar Mis Facturas.  Aquí encontraras una opción para que elijas tu configuración de alertas.</p></td></tr></table></body></html>";
                            plainBody = "Estimado Cliente.Agradecemos tu compra.Encuentra en este correo el detalle de la factura que acabamos de emitirte:" + url;
                        }

                        AlternateView plainView = AlternateView.CreateAlternateViewFromString(plainBody, null, "text/plain");
                        AlternateView htmlView = AlternateView.CreateAlternateViewFromString(body, null, "text/html");

                        htmlView.LinkedResources.Add(lr);

                        message.AlternateViews.Add(plainView);
                        message.AlternateViews.Add(htmlView);


                        smtpClient.Send(message);
                        enviadomail = 1;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.ReadKey();
                        enviadomail = 3;
                    }


                }
                else
                {
                    if (emailcliente == "")
                    {
                        enviadomail = 4;
                    }
                    else
                    {
                        enviadomail = 2;
                    }
                }

                linea.DocNum = singlefact.Docnum;
                linea.EnviadoMail = enviadomail;

                electrodb.TBL_NOTIFICACION_FACT.InsertOnSubmit(linea);
                electrodb.SubmitChanges();

            }
            

        }
    }
}
