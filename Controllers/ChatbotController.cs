using Microsoft.AspNetCore.Mvc;
using Twilio.TwiML;
using Twilio.TwiML.Messaging;
using System.Collections.Concurrent;

namespace ChatbotCobranzaMovil.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ChatbotController : ControllerBase
    {
        private static ConcurrentDictionary<string, Conversacion> conversaciones = new();

        [HttpPost]
        public IActionResult RecibirMensaje([FromForm] string From, [FromForm] string Body)
        {
            var respuesta = new MessagingResponse();

            var telefono = From;
            var mensaje = Body?.Trim().ToLower() ?? "";

            if (!conversaciones.ContainsKey(telefono))
                conversaciones[telefono] = new Conversacion();

            var estado = conversaciones[telefono];

            switch (estado.Paso)
            {
                case 0:
                    respuesta.Message("¡Hola! Ingresa tu número de ruta:");
                    estado.Paso = 1;
                    break;

                case 1:
                    estado.Ruta = mensaje.Replace("ruta", "").Trim();
                    respuesta.Message("Si necesitas permiso para reimpresión escribe 1, o si necesitas permiso para cancelación, escribe 2:");
                    estado.Paso = 2;
                    break;

                case 2:
                    if (mensaje == "1" || mensaje == "2")
                    {
                        estado.TipoPermiso = mensaje == "1" ? "reimpresion" : "cancelacion";
                        respuesta.Message("Explica el motivo:");
                        estado.Paso = 3;
                    }
                    else
                    {
                        respuesta.Message("Por favor, escribe 1 para reimpresión o 2 para cancelación.");
                    }
                    break;

                case 3:
                    estado.Motivo = mensaje;

                    try
                    {
                        var firebase = new ccFirebase20();
                        string ruta = estado.Ruta;
                        string tipo = estado.TipoPermiso == "reimpresion" ? "reimpresiones" : "cancelaciones";
                        string id = Guid.NewGuid().ToString();

                        var data = new
                        {
                            tipoPermiso = estado.TipoPermiso,
                            fecha = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            motivo = estado.Motivo
                        };

                        var infoResponse = firebase.client.Set($"InfoPermisos/{ruta}/{id}", data);
                        var permisoResponse = firebase.client.Set($"Permisos/{ruta}/{tipo}", "1");

                        if (infoResponse.StatusCode.ToString() == "OK" && permisoResponse.StatusCode.ToString() == "OK")
                        {
                            respuesta.Message("✅ Permiso otorgado exitosamente. ¡Hasta luego!");
                        }
                        else
                        {
                            respuesta.Message("❌ Hubo un problema al otorgar el permiso. Intenta nuevamente.");
                        }
                    }
                    catch (Exception ex)
                    {
                        respuesta.Message("⚠️ Ocurrió un error al otorgar el permiso. Intenta más tarde.");
                        Console.WriteLine("Error Firebase: " + ex.Message);
                    }

                    conversaciones.TryRemove(telefono, out _);
                    break;

                default:
                    respuesta.Message("Algo salió mal. Escribe 'Hola' para empezar de nuevo.");
                    conversaciones.TryRemove(telefono, out _);
                    break;
            }

            return Content(respuesta.ToString(), "application/xml");
        }

        public class Conversacion
        {
            public int Paso { get; set; } = 0;
            public string Ruta { get; set; }
            public string TipoPermiso { get; set; }
            public string Motivo { get; set; }
        }
    }
}
