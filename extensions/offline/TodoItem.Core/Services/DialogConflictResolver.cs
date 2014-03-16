using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.WindowsAzure.MobileServices.Caching;

namespace Todo
{
    public class DialogConflictResolver : IConflictResolver
    {
        public Task<ConflictResult> Resolve(Conflict conflict)
        {
            ConflictResult result = new ConflictResult();
            TaskCompletionSource<int> task = new TaskCompletionSource<int>();

            ShowDialogMessage message = new ShowDialogMessage("CONFLICT DETECTED - Select a resolution:",
                string.Format("Server Text: \"{0}\" \nLocal Text: \"{1}\"\n",
                            conflict.CurrentItem.Value<string>("text"), conflict.NewItem.Value<string>("text")),
                new[] {
                    new Button("Local Text", b =>
                    {
                        conflict.NewItem["__version"] = conflict.Version;
                        result.ModifiedItems.Add(conflict.NewItem);
                        task.SetResult(0);
                    }),
                    new Button("Server Text", b =>
                    {
                        conflict.CurrentItem["__version"] = conflict.Version;
                        result.ModifiedItems.Add(conflict.CurrentItem);
                        task.SetResult(0);
                    })
                });

            Messenger.Default.Send(message);

            return task.Task.ContinueWith(t => result, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }
}
