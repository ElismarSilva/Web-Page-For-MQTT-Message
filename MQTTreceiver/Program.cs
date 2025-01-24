using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System.Net;
using System.Text;
using System.Web;  // Adicione esta referência

class Program
{
    private static IMqttClient mqttClient;
    private static string ultimaMensagemRecebida = "Nenhuma mensagem recebida ainda.";
    private static string ultimaMensagemEnviada = "Nenhuma mensagem enviada ainda.";

    static async Task Main(string[] args)
    {
        var factory = new MqttFactory();
        mqttClient = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("test.mosquitto.org", 1883) // Endereço do broker MQTT
            .WithClientId("SubscriberClient")          // Identificador do cliente
            .Build();

        // Handler para mensagens recebidas
        mqttClient.UseApplicationMessageReceivedHandler(e =>
        {
            string topic = e.ApplicationMessage.Topic;
            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

            ultimaMensagemRecebida = $"Tópico: {topic} | Mensagem: {payload}";
            Console.WriteLine($"Mensagem recebida:");
            Console.WriteLine($"Tópico: {topic}");
            Console.WriteLine($"Mensagem: {payload}");
        });

        // Handler para conexão
        mqttClient.UseConnectedHandler(async e =>
        {
            Console.WriteLine("Conectado ao broker MQTT!");

            // Assinar um tópico
            string topic = "Csharp/MQTT";
            await mqttClient.SubscribeAsync(topic);
            Console.WriteLine($"Inscrito no tópico: {topic}");
        });

        // Handler para desconexão
        mqttClient.UseDisconnectedHandler(e =>
        {
            Console.WriteLine("Desconectado do broker MQTT!");
        });

        // Conectar ao broker MQTT
        await mqttClient.ConnectAsync(options);

        // Iniciar o servidor web
        await IniciarServidorWeb();
    }

    private static async Task IniciarServidorWeb()
    {
        // Configura o servidor HTTP
        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/");
        listener.Start();

        Console.WriteLine("Servidor Web iniciado em http://localhost:8080");

        while (true)
        {
            // Aguarda uma solicitação
            HttpListenerContext context = await listener.GetContextAsync();

            // HTML estilizado para exibir mensagens
            string responseString = $@"
            <html>
                <head>
                    <meta charset='UTF-8'>
                    <title>MQTT Mensagens</title>
                    <style>
                        body {{
                            font-family: Arial, sans-serif;
                            background-color: #f4f4f9;
                            color: #333;
                            margin: 0;
                            padding: 20px;
                        }}
                        h1 {{
                            text-align: center;
                            color: #555;
                        }}
                        .container {{
                            max-width: 800px;
                            margin: 0 auto;
                            padding: 20px;
                            background: #fff;
                            box-shadow: 0 0 10px rgba(0, 0, 0, 0.1);
                            border-radius: 8px;
                        }}
                        .message {{
                            margin-bottom: 20px;
                        }}
                        .message h2 {{
                            font-size: 1.2em;
                            color: #444;
                        }}
                        .message p {{
                            background: #e7f3fe;
                            padding: 10px;
                            border: 1px solid #d0e7fd;
                            border-radius: 5px;
                            overflow-wrap: break-word;
                        }}
                        .send-message {{
                            margin-top: 20px;
                        }}
                        .send-message input {{
                            width: calc(100% - 120px);
                            padding: 10px;
                            font-size: 16px;
                            border: 1px solid #ddd;
                            border-radius: 5px;
                        }}
                        .send-message button {{
                            padding: 10px 15px;
                            font-size: 16px;
                            background-color: #4CAF50;
                            color: white;
                            border: none;
                            border-radius: 5px;
                            cursor: pointer;
                        }}
                        footer {{
                            text-align: center;
                            margin-top: 20px;
                            color: #888;
                        }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <h1>MQTT Mensagens</h1>
                        <div class='message'>
                            <h2>Última Mensagem Recebida</h2>
                            <p>{ultimaMensagemRecebida}</p>
                        </div>
                        <div class='message'>
                            <h2>Última Mensagem Enviada</h2>
                            <p>{ultimaMensagemEnviada}</p>
                        </div>

                        <div class='send-message'>
                            <h2>Enviar Mensagem</h2>
                            <form method='POST' action='/send'>
                                <input type='text' name='message' placeholder='Digite sua mensagem' required/>
                                <button type='submit'>Enviar</button>
                            </form>
                        </div>
                    </div>
                    <footer> &copy Elismar Silva - 2025</footer>
                </body>
            </html>";

            // Verifica se é uma requisição POST (formulário enviado)
            if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/send")
            {
                // Lê a mensagem enviada pelo formulário
                string message = await new StreamReader(context.Request.InputStream).ReadToEndAsync();
                message = HttpUtility.UrlDecode(message.Split('=')[1]); // Decodifica a URL e extrai a mensagem

                if (!string.IsNullOrEmpty(message))
                {
                    // Publica a mensagem recebida no tópico MQTT
                    var mqttMessage = new MqttApplicationMessageBuilder()
                        .WithTopic("Csharp/MQTT")
                        .WithPayload(message)
                        .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                        .Build();

                    await mqttClient.PublishAsync(mqttMessage);
                    ultimaMensagemEnviada = $"Tópico: Csharp/MQTT | Mensagem: {message}";
                }

                // Redireciona para recarregar a página
                context.Response.Redirect("/");
            }

            // Garante a codificação UTF-8
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            context.Response.ContentLength64 = buffer.Length;
            context.Response.ContentType = "text/html; charset=UTF-8";
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
        }
    }
}
