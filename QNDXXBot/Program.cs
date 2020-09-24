using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GammaLibrary;
using GammaLibrary.Extensions;
using Mirai_CSharp;
using Mirai_CSharp.Extensions;
using Mirai_CSharp.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using TextCommandCore;

namespace QNDXXBot
{
    public static class Program
    {
        public static MiraiHttpSession mirai;
        private static List<Student> PreRegisteredStudents = new List<Student>();

        static async Task Main(string[] args)
        {
            var lines = File.ReadAllLines("students.csv", Encoding.UTF8);
            foreach (var line in lines.Where(l => l.NotNullNorWhiteSpace()))
            {
                var sp = line.Split(',');
                var id = sp[0];
                var stdid = sp[1];
                var name = sp[2];
                PreRegisteredStudents.Add(new Student(null, id.ToInt(), stdid, name));
            }

            var options = new MiraiHttpSessionOptions("127.0.0.1", 8080, "INITKEY7d0dck9h");
            var qq = 3320645904;
            mirai = new MiraiHttpSession();

            await mirai.ConnectAsync(options, qq);
            mirai.FriendMessageEvt += OnFriendMessage;
            mirai.NewFriendApplyEvt += (sender, eventArgs) =>
            {
                mirai.HandleNewFriendApplyAsync(eventArgs, FriendApplyAction.Allow, "略略略");
                Thread.Sleep(5000);
                SendPrivate(eventArgs.FromQQ.ToString(), $"使用[注册 名字]来注册 如输入 '注册 小明' \n" +
                                                         $"使用[上传图片]来开始上传图片, 在输入上传图片后 发送的下一张图片将会被保存.\n" +
                                                         $"以上内容均不包含括号'[]'\n" +
                                                         $"Written by Cyl18 2020 https://github.com/Cyl18/");
                return Task.FromResult(true);
            };
            mirai.DisconnectedEvt += async (sender, exception) =>
            {
                while (true)
                {
                    try
                    {
                        await mirai.ConnectAsync(options, qq); // 连到成功为止, QQ号自填, 你也可以另行处理重连的 behaviour
                        return true;
                    }
                    catch (Exception)
                    {
                        await Task.Delay(1000);
                    }
                }
            };

            while (true)
            {
                if (await Console.In.ReadLineAsync() == "exit")
                {
                    return;
                }
            }
        }

        private static HashSet<Student> QQsReceivesImageSet = new HashSet<Student>();
        private static async Task<bool> OnFriendMessage(MiraiHttpSession sender, IFriendMessageEventArgs e)
        {
            var qq = e.Sender.Id;
            var msg = e.Chain.GetPlain();

            var (matched, result) = new PrivateMessageHandler(qq, msg).ProcessCommandInput();

            var img = e.Chain.OfType<ImageMessage>().FirstOrDefault();
            if (img != null)
            {
                if (QQsReceivesImageSet.Any(s => s.QQ == qq.ToString()))
                {
                    var student = QQsReceivesImageSet.First(s => s.QQ == qq.ToString());
                    QQsReceivesImageSet.RemoveWhere(s => s.QQ == qq.ToString());
                    var url = img.Url;
                    var client = new HttpClient();
                    var image = await client.GetByteArrayAsync(url);

                    if (ImageManager.ExistsAndIfRemove(student))
                    {
                        SendPrivate(qq.ToString(), "注意: 你已经上传过照片, 这张照片将被覆盖");
                    }

                    if (ImageManager.GetExtension(image) == null)
                    {
                        SendPrivate(qq.ToString(), "图片格式不支持.");
                        return true;
                    }

                    ImageManager.SaveImage(student, image);
                    SendPrivate(qq.ToString(), "图片成功保存!");
                }
                else
                {
                    if (!Config.Instance.SentImageHint.Contains(qq.ToString()))
                    {
                        SendPrivate(qq.ToString(), "图片不能直接发送, 输入 help 或 帮助 来查看使用方法.");
                        Config.Instance.SentImageHint.Add(qq.ToString());
                        Config.Save();
                    }
                }
            }


            return true;
        }

        private static void SendPrivate(string qq, Message msg) => mirai.SendFriendMessageAsync(qq.ToLong(), new PlainMessage(msg.Content));



        public static class ImageManager
        {
            public static void SaveImage(Student student, byte[] image)
            {
                File.WriteAllBytes(Path.Combine(Config.Instance.CurrentTaskID.ToString(), student + GetExtension(image)), image);
            }
            public static bool Exists(Student student)
            {
                var name = student.ToString();
                var files = Directory.GetFiles(Config.Instance.CurrentTaskID.ToString(), $"{name}.*");

                return files.Any();
            }

            public static bool ExistsAndIfRemove(Student student)
            {
                var name = student.ToString();
                var files = Directory.GetFiles(Config.Instance.CurrentTaskID.ToString(), $"{name}.*");
                if (files.Any())
                {
                    File.Delete(files.First());
                }
                return files.Any();
            }

            public static string GetExtension(byte[] bytes)
            {
                switch (Image.DetectFormat(bytes))
                {
                    case GifFormat gifFormat:
                        return null;
                    case JpegFormat jpegFormat:
                        return ".jpg";
                    case PngFormat pngFormat:
                        return ".png";
                    default:
                        return null;
                }
            }
        }

        public partial class PrivateMessageHandler : ICommandHandler<PrivateMessageHandler>
        {
            [Matchers("增加ID")]
            [RequireAdmin]
            string AddTargetID()
            {
                Config.Instance.CurrentTaskID++;
                Config.Save();
                Directory.CreateDirectory(Config.Instance.CurrentTaskID.ToString());
                return $"设置完成. 当前ID是{Config.Instance.CurrentTaskID}";
            }

            [Matchers("获取当前ID")]
            string GetTargetID()
            {
                return Config.Instance.CurrentTaskID.ToString();
            }

            [RequireAdmin]
            [Matchers("导出全部学生")]
            string DumpStudents()
            {
                return Config.Instance.Students.Select(student => $"[{student.ID}] {student.Name}: {student.QQ}").Connect("\n");
            }

            [Matchers("帮助", "help")]
            string Help()
            {
                return $"使用[注册 名字]来注册 如输入 '注册 小明' \n" +
                       $"使用[上传图片]来开始上传图片, 在输入上传图片后 发送的下一张图片将会被保存.\n" +
                       $"以上内容均不包含括号'[]'\n" +
                       $"Written by Cyl18 2020 https://github.com/Cyl18/";
            }

            [Matchers("注册")]
            string Register(string name)
            {
                var student = PreRegisteredStudents.Find(s => s.Name == name);
                if (student == null)
                {
                    return "没有找到你的注册信息. 请检查你的名字是否正确?";
                }
                else
                {
                    if (Config.Instance.Students.Find(s => s.Name == name) != null)
                    {
                        return "你已经注册了. 请联系管理员注销账号.";
                    }

                    Config.Instance.Students.Add(new Student(Sender.ID.ToString(), student.ID, student.StudentNumber, student.Name));
                    Config.Save();
                    return $"注册成功. 你的学号应该是{student.StudentNumber}.";
                }
            }



            [Matchers("通知")]
            [RequireAdmin]
            string Notify()
            {
                Task.Factory.StartNew(() =>
                {
                    foreach (var student in Config.Instance.Students)
                    {
                        if (!ImageManager.Exists(student))
                        {
                            SendPrivate(student.QQ, "提醒: 该交青年大学习啦~");
                            Thread.Sleep(TimeSpan.FromSeconds(10));
                        }

                    }
                    SendPrivate(Sender.ID.ToString(), "执行完成");
                }, TaskCreationOptions.LongRunning);

                return "正在执行. 每个人会延迟10s";
            }

            [Matchers("未交名单")]
            string GetUnsubmittedList()
            {
                return $"注册过的人有这些没交: {Config.Instance.Students.Where(student => !ImageManager.Exists(student)).Connect()}";
            }

            [RequireAdmin]
            [Matchers("注销")]
            string Deregister(string id)
            {
                Config.Instance.Students.RemoveAll(s => s.ID == id.ToInt());
                Config.Save();
                return "移除成功.";
            }

            [Matchers("上传图片")]
            string UploadImage()
            {
                var student = Config.Instance.Students.Find(s => s.QQ == Sender.ID.ToString());
                if (student == null)
                {
                    return "没有找到你. 请先使用 [注册 姓名] 如 [注册 小明]";
                }

                QQsReceivesImageSet.Add(student);
                return "下一张上传的图片将会被上传..";
            }

            [RequireAdmin]
            [Matchers("校验学号")]
            string FindNotAdded()
            {
                var sb = new StringBuilder("以下学生没有添加用户.");
                var flag = false;
                foreach (var student in PreRegisteredStudents)
                {
                    if (Config.Instance.Students.All(s => s.Name != student.Name))
                    {
                        flag = true;
                        sb.Append($"{student}, ");
                    }
                }

                return flag ? sb.ToString() : "所有人都添加了.";
            }

            public Action<TargetID, Message> MessageSender { get; } = (id, msg) => SendPrivate(id.ID, msg);


            public Action<Message> ErrorMessageSender { get; } = msg => SendPrivate("775942303", msg);
            public UserID Sender { get; }
            public string Message { get; }

            string ICommandHandler<PrivateMessageHandler>.Sender => Sender;

            public PrivateMessageHandler(UserID sender, string message)
            {
                Sender = sender;
                Message = message;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RequireAdminAttribute : Attribute, IPreProcessor
    {
        public string Process<T>(MethodInfo method, string msg, ICommandHandler<T> handlers) where T : ICommandHandler<T>
        {
            if (handlers is Program.PrivateMessageHandler h && !(h.Sender.ToString() == "393575404" || h.Sender.ToString() == "775942303")) throw new CommandException("你不是管理.");

            return msg;
        }
    }

    [Configuration("config")]
    public class Config : Configuration<Config>
    {
        public List<Student> Students = new List<Student>();
        public List<string> SentImageHint = new List<string>();
        public int CurrentTaskID = 0;

    }

    public class Student
    {
        public string QQ { get; set; }
        public int ID { get; set; }
        public string StudentNumber { get; set; }
        public string Name { get; set; }

        protected bool Equals(Student other)
        {
            return ID == other.ID;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Student)obj);
        }

        public override int GetHashCode()
        {
            return ID;
        }

        public Student(string qq, int id, string studentNumber, string name)
        {
            QQ = qq;
            ID = id;
            StudentNumber = studentNumber;
            Name = name;
        }

        public override string ToString()
        {
            return $"{Name}{StudentNumber}";
        }
    }


    public struct GroupID
    {
        public uint ID { get; }

        public GroupID(uint id)
        {
            ID = id;
        }

        public static implicit operator long(GroupID id)
        {
            return id.ID;
        }

        public static implicit operator uint(GroupID id)
        {
            return id.ID;
        }

        public static implicit operator string(GroupID id)
        {
            return id.ToString();
        }

        public static implicit operator GroupID(long id)
        {
            return new GroupID((uint)id);
        }

        public static implicit operator GroupID(uint id)
        {
            return new GroupID(id);
        }

        public static implicit operator GroupID(string id)
        {
            return new GroupID(id.ToUInt());
        }

        public override string ToString()
        {
            return ID.ToString();
        }
    }

    public struct UserID
    {
        public uint ID { get; }

        public UserID(uint id)
        {
            ID = id;
        }

        public static implicit operator long(UserID id)
        {
            return id.ID;
        }

        public static implicit operator uint(UserID id)
        {
            return id.ID;
        }

        public static implicit operator string(UserID id)
        {
            return id.ToString();
        }

        public static implicit operator UserID(long id)
        {
            return new UserID((uint)id);
        }

        public static implicit operator UserID(uint id)
        {
            return new UserID(id);
        }

        public static implicit operator UserID(string id)
        {
            return new UserID(id.ToUInt());
        }

        public override string ToString()
        {
            return ID.ToString();
        }
    }
}
