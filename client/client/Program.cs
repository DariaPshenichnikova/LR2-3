using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace client
{
    class Program
    {
        // все кроме Main так же как и в сервере
        static int port = 8001;
        static string address = "127.0.0.1";

        //состояние одного пакета (камень, ножницы, бцмага, победа, поражение, ничья, ошибка)
        public enum enumState { stone, screes, paper, win, loos, draw, error };

        //функция возвращает название состояния по его коду
        public static string nameState(enumState state)
        {
            switch (state)
            {
                case enumState.stone:
                    return "камень";
                case enumState.screes:
                    return "ножницы";
                case enumState.paper:
                    return "бумага";
                case enumState.win:
                    return "победа";
                case enumState.loos:
                    return "поражение";
                case enumState.error:
                    return "ошибка";
                case enumState.draw:
                    return "ничья";
                default:
                    return "ошибка";
            }
        }

        //функция для написания красивого лога
        static void log(string message)
        {
            Console.WriteLine(DateTime.Now.ToString() + ": " + message);
        }

        //класс передаваемого состояния
        public class Packet
        {
            //схема валидации. проперти - поля, type - тип объекта, enum - допустимые значения поля, required - обяхательные поля
            public static JSchema schema = JSchema.Parse(
                @"{" +
                    "'type': 'object', " +
                    "'properties': " +
                    "{" +
                        "'State':  " +
                        "{" +
                            "'type':'integer', " +
                            "'enum': [0,1,2,3,4,5,6]" +
                        "}" +
                    "}, " +
                    "'required': ['State']" +
                "}");

            // поле, хранящее состояние
            public enumState State { get; set; }

            // конструктор
            public Packet(enumState state)
            {
                State = state;
            }

            //десериализация из json'a
            public static Packet fromJson(string json)
            {
                // объект читающий из json
                JsonTextReader reader = new JsonTextReader(new StringReader(json));
                // объект, оборачивающий читающий из json, Дополнительно валидирует с помощью схемы
                JSchemaValidatingReader validatingReader = new JSchemaValidatingReader(reader);
                validatingReader.Schema = schema;
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<Packet>(validatingReader);
            }

            //сериализация в json
            public string toJson()
            {
                StringWriter sw = new StringWriter();
                JsonTextWriter writer = new JsonTextWriter(sw);
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(writer, this);
                return sw.ToString();
            }
        }
        static void Main(string[] args)
        {
            try
            {
                IPEndPoint ipPoint = new IPEndPoint(IPAddress.Parse(address), port);
                log("Ожидаем подключения");
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                // подключаемся к удаленному хосту
                socket.Connect(ipPoint);
                log("Подключение произведено");
                Packet packet = new Packet(enumState.error);
                int t = -1;
                byte[] data = new byte[256];
                StringBuilder builder = new StringBuilder();
                do
                {
                    t = -1;
                    //пользователь вводит свой ход
                    do
                    {
                        Console.Write("Сделайте ход (камень-0, ножницы-1, бумага-2)> ");
                        try
                        {
                            t = Int32.Parse(Console.ReadLine());
                        }
                        catch(Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    } while (t != (int)enumState.stone && t != (int)enumState.screes && t != (int)enumState.paper);
                    packet.State = (enumState)t;
                    // отправляем пакет
                    socket.Send(Encoding.Unicode.GetBytes(packet.toJson()));
                    log("Данные отправлены на сервер, ожидается ответ сервера");

                    //считываем ответ
                    t = socket.Receive(data);
                    //переводим  в строку
                    builder.Append(Encoding.Unicode.GetString(data, 0, t));
                    //парсим объект из json'a
                    packet = Packet.fromJson(builder.ToString());
                    //очищаем билдер строк
                    builder.Clear();
                    log("Получен ответ сервера, результат игры: " + nameState(packet.State));
                } while (packet.State != enumState.error);
                
                // закрываем сокет
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Попытка повторного подключения");
                Main(args);
            }
            Console.Read();
        }
    }
}
