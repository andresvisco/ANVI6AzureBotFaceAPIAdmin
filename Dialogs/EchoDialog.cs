using System;
using System.Threading.Tasks;

using Microsoft.Bot.Connector;
using Microsoft.Bot.Builder.Dialogs;
using System.Net.Http;
using System.Collections.Generic;
using System.IO;
using Microsoft.ProjectOxford.Face;
using System.Linq;

namespace Microsoft.Bot.Sample.SimpleEchoBot
{
    [Serializable]
    public class EchoDialog : IDialog<object>
    {
        protected int count = 1;
        public string subscriptionKey = "5dba748bf55e4741b06ece38a85cbcce";
        public string subscriptionEndpoint = "https://eastus.api.cognitive.microsoft.com/face/v1.0";
        public string guidPersona = string.Empty;

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
        }
        string nombreUsuario;
        private bool userWelcomed;


        public async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
        {
            var message = await argument;

            context.UserData.TryGetValue("username",out nombreUsuario).ToString();

            //await context.PostAsync(nombreUsuario);

            if (nombreUsuario == null)
            {
                PromptDialog.Text(context, SeguirDespuesNombre, "Antes de continuar, podría preguntarle su nombre?");
                return;
            }
            else { 
                userWelcomed = true;


                PromptDialog.Text(context, SeguirDespuesNombre2, nombreUsuario + ", en que puedo ayudarte");

            }


        }
        
        public async Task SeguirDespuesNombre2(IDialogContext context, IAwaitable<string> result)
        {
            var ayuda = await result;
            var accion = ayuda.ToLower().ToString();
            int accionTrim = 0;
            if (accion.Contains("alta"))
            {
                accionTrim = 1;
            }
            else if (accion.Contains("validar"))
            {
                accionTrim = 2;
            }
            else if (accion.Contains("deuda")||accion.Contains("saldo"))
            {
                accionTrim = 3;
            }

            switch (accionTrim)
            {
                case 1:
                    PromptDialog.Text(context, PedirNombre, "Ingresar nombre a dar de alta");
                    break;
                case 2:
                    PromptDialog.Attachment(context, PedirImagen, "Subir imagen a validar");
                    break;
                case 3:
                    PromptDialog.Confirm(context,
                    ConfirmaPedidoDeuda,
                    "Entiendo que quieres consultar un estado de deuda, esto es correcto?", "",
                    promptStyle: PromptStyle.Auto);
                    break;
                default:
                    await context.PostAsync("Solamente estoy preparado para asistir en casos de consulta de deudas, por lo cual espero poder ayudarte la próxima vez. Gracias! :)");
                    break;
            };
           
            
           

                
            
            
            
            

        }
        public async Task PedirNombre(IDialogContext context, IAwaitable<string> argument)
        {
            var nombre = await argument;
            await context.PostAsync("El nombre elegido es: " + nombre);
            FaceServiceClient faceServiceClient = new FaceServiceClient(subscriptionKey, subscriptionEndpoint);

            var PersonaCreada = await faceServiceClient.CreatePersonInLargePersonGroupAsync("1", nombre.ToString());
            guidPersona = PersonaCreada.PersonId.ToString();
            PromptDialog.Attachment(context, ValidarImagen, "Subir Imagen");
            
        }
        public async Task PedirImagen(IDialogContext context, IAwaitable<IEnumerable<Attachment>> argument)
        {
            var imagen = await argument;
            HttpClient cliente = new HttpClient();

            foreach (var pic in imagen)
            {
                var url = pic.ContentUrl;
                var dato = await cliente.GetByteArrayAsync(url);
                Stream stream = new MemoryStream(dato);
                FaceServiceClient faceServiceClient = new FaceServiceClient(subscriptionKey, subscriptionEndpoint);
                var faces = await faceServiceClient.DetectAsync(stream, true, false, null);

                var resultadoIdentifiacion = await faceServiceClient.IdentifyAsync(faces.Select(ff => ff.FaceId).ToArray(), largePersonGroupId: "1");
                var res = resultadoIdentifiacion[0];
                if (res.Candidates.Length>0)
                {
                    try
                    {
                        var nombrePersona = await faceServiceClient.GetPersonInLargePersonGroupAsync("1", new Guid(res.Candidates[0].PersonId.ToString()));
                        var nombreText = nombrePersona.Name.ToString();
                        await context.PostAsync("La persona es: " + nombreText.ToString());
                    }
                    catch (Exception ex)
                    {
                        await context.PostAsync("No se encontró la identidad de la persona. Para darla de alta utilizar el comando: \"alta\"");
                        
                    }
                    

                }
            }
        }
        public async Task ValidarImagen(IDialogContext context, IAwaitable<IEnumerable<Attachment>> argument)
        {
            var imagen = await argument;
            HttpClient cliente = new HttpClient();

            foreach(var pic in imagen)
            {
                var url = pic.ContentUrl;
                var dato = await cliente.GetByteArrayAsync(url);
                Stream stream = new MemoryStream(dato);
                FaceServiceClient faceServiceClient = new FaceServiceClient(subscriptionKey, subscriptionEndpoint);
                
                var faces = await faceServiceClient.AddPersonFaceInLargePersonGroupAsync("1",new Guid(guidPersona), stream, null, null);
                var entrenamiento = faceServiceClient.TrainLargePersonGroupAsync("1");
                var status = entrenamiento.Status.ToString();

                await context.PostAsync("Estado entrenamiento: " + status + " PersonGuid: "+ faces.PersistedFaceId.ToString());
            }

        }

        
        

        public async Task SeguirDespuesNombre(IDialogContext context, IAwaitable<string> result)
        {
            try {
                var nombreUsuario = await result;
                userWelcomed = true;
                context.UserData.SetValue("username", nombreUsuario);
                await context.PostAsync($"Bienvenido {nombreUsuario}!");
                PromptDialog.Text(context, SeguirDespuesNombre2, "En que puedo ayudarte");


            }
            catch (TooManyAttemptsException ex)
            {
                await context.PostAsync($"Demasiados intentos :( Technical Details: {ex}");
            }

        }
        public async Task IngresoNumeroCliente(IDialogContext context, IAwaitable<IMessageActivity> argument)
        {
            var cliente_arg = await argument;
            var cliente = cliente_arg.Text;
            var user = nombreUsuario;
            if (cliente == "1234")
            {


                await context.PostAsync("Bienvenido de vuelta "+ nombreUsuario +"! Su deuda corresponde a $ 1500");
                    PromptDialog.Choice(context,
                        ElegirOpcion,
                        (IEnumerable<Opciones>)Enum.GetValues(typeof(Opciones)),
                        "Por favor seleccione la opción deseada",
                        "Opción no disponible",
                        promptStyle: PromptStyle.Auto
                        );
                
                
                //context.Wait(MessageReceivedAsync);

            }
            else
            {
                await context.PostAsync("No reconozco el número de cliente. Intenta nuevamente por favor");
                context.Wait(IngresoNumeroCliente);
            }
        }
        public enum Opciones
        {
            PagarConTarjeta,
            CancelarEnCentroDePago,
            Cancelar
        }
        public async Task ElegirOpcion(IDialogContext context, IAwaitable<Opciones> argument)
        {
            var opcion = await argument;
            if (opcion.ToString() == "Cancelar")
            {
                await context.PostAsync("Muchas gracias por su consulta");
                context.Done(this);
            }
            else { 

            await context.PostAsync("Opcion seleccionada: " + opcion);
            context.Done(this);
            }
        }

        public async Task ConfirmaPedidoDeuda(IDialogContext context, IAwaitable<bool> argument)
        {
            var confirm = await argument;
            if (confirm)
            {
                await context.PostAsync("Ingrese su numero de cliente");
                context.Wait(IngresoNumeroCliente);
            }
            else
            {
                await context.PostAsync("Canceló la operación");
                context.Done(this);

            }
        }

        public async Task AfterResetAsync(IDialogContext context, IAwaitable<bool> argument)
        {
            var confirm = await argument;
            if (confirm)
            {
                this.count = 1;
                await context.PostAsync("Reset count.");
            }
            else
            {
                await context.PostAsync("Did not reset count.");
            }
            context.Wait(MessageReceivedAsync);
        }

    }
}