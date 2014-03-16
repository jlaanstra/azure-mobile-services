using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;

namespace Todo
{
    public class ShowDialogMessage : MessageBase
    {
        public ShowDialogMessage(string title, string content, IEnumerable<Button> buttons)
        {
            this.Title = title;
            this.Content = content;
            this.Buttons = buttons;
        }

        public string Title { get; private set; }

        public string Content { get; private set; }

        public IEnumerable<Button> Buttons { get; private set; }
    }

    public class Button
    {
        public Button(string title, Action<Button> callback)
        {
            this.Title = title;
            this.CallBack = callback;
        }
        public string Title { get; private set; }

        public Action<Button> CallBack { get; private set; }
    }
}
