using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Caching;
using Newtonsoft.Json;

namespace Todo.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IMobileServiceClient client;
        private readonly NetworkInformationDelegate networkDelegate;

        private readonly IMobileServiceTable<TodoItem> todoTable;

        private IDictionary<string, string> param = new Dictionary<string, string>() { { "resolveStrategy", "client" } };
        private IDictionary<string, string> empty = new Dictionary<string, string>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public MainViewModel(IMobileServiceClient client, NetworkInformationDelegate networkDelegate)
        {
            this.client = client;
            this.networkDelegate = networkDelegate;

            this.todoTable = client.GetTable<TodoItem>();

            this.RefreshCommand = new RelayCommand(async() => await this.RefreshTodoItems());
            this.SaveCommand = new RelayCommand(async () => await this.SaveTodoItem());
            this.CompletedCommand = new RelayCommand<TodoItem>(async x =>
            {
                x.Complete = true;
                await this.UpdateTodoItem(x);
                this.Items.Remove(x);
            });
            this.UpdateTextCommand = new RelayCommand<TodoItem>(async x => await this.UpdateTodoItem(x));
            this.RemoveCommand = new RelayCommand<TodoItem>(async x => await this.RemoveTodoItem(x));

            this.Items = new ObservableCollection<TodoItem>();
        }

        public RelayCommand RefreshCommand { get; set; }
        public RelayCommand<TodoItem> RemoveCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }
        public RelayCommand<TodoItem> UpdateTextCommand { get; set; }
        public RelayCommand<TodoItem> CompletedCommand { get; set; }

        private ObservableCollection<TodoItem> items;
        public ObservableCollection<TodoItem> Items
        {
            get { return this.items; }
            set { this.Set(ref items, value, "Items"); }
        }

        private string text;
        public string Text
        {
            get { return this.text; }
            set { this.Set(ref text, value, "Text"); }
        }

        public bool IsOnline
        {
            get { return networkDelegate.IsOnline; }
            set { this.networkDelegate.IsOnline = value; }
        }

        private bool conflictsOnClient;
        public bool ConflictsOnClient
        {
            get { return this.conflictsOnClient; }
            set { this.Set(ref conflictsOnClient, value); }
        }

        private async Task RefreshTodoItems()
        {
            MobileServiceConflictsResolvedException exception = null;
            try
            {
                // This code refreshes the entries in the list view be querying the TodoItems table.
                // The query excludes completed TodoItems
                Items = await todoTable
                    .Where(todoItem => todoItem.Complete == false)
                    .IncludeTotalCount()
                    .WithParameters(ConflictsOnClient ? param : empty)
                    .ToCollectionAsync();
            }
            catch (MobileServiceInvalidOperationException e)
            {
                Messenger.Default.Send(new ShowDialogMessage("Error loading items", exception.Message, new[] { new Button("OK", b => { }) }));
                Items = null;
            }
            catch (MobileServiceConflictsResolvedException e)
            {
                exception = e;
            }

            if (exception != null)
            {
                await this.RefreshTodoItems();
            }
        }

        private async Task SaveTodoItem()
        {
            var todoItem = new TodoItem { Text = this.Text };

            MobileServiceConflictsResolvedException exception = null;
            try
            {
                // This code inserts a new TodoItem into the database. When the operation completes
                // and Mobile Services has assigned an Id, the item is added to the CollectionView
                await todoTable.InsertAsync(todoItem, ConflictsOnClient ? param : empty);
                Items.Add(todoItem);
            }
            catch (MobileServiceInvalidOperationException e)
            {
                Messenger.Default.Send(new ShowDialogMessage("Error inserting item", exception.Message, new[] { new Button("OK", b => { }) }));
            }
            catch (MobileServiceConflictsResolvedException e)
            {
                exception = e;
            }

            if (exception != null)
            {
                await this.RefreshTodoItems();
            }
        }

        private async Task UpdateTodoItem(TodoItem item)
        {
            MobileServiceConflictsResolvedException exception = null;
            try
            {
                // This code takes a freshly completed TodoItem and updates the database. When the MobileService 
                // responds, the item is removed from the list 
                await todoTable.UpdateAsync(item, ConflictsOnClient ? param : empty);
            }
            catch (MobileServiceInvalidOperationException e)
            {
                Messenger.Default.Send(new ShowDialogMessage("Error updating item", exception.Message, new[] { new Button("OK", b => { }) }));
            }
            catch (MobileServiceConflictsResolvedException e)
            {
                exception = e;
            }

            if (exception != null)
            {
                await this.RefreshTodoItems();
            }
        }

        private async Task RemoveTodoItem(TodoItem item)
        {
            MobileServiceConflictsResolvedException exception = null;
            try
            {
                // This code takes a freshly completed TodoItem and updates the database. When the MobileService 
                // responds, the item is removed from the list 
                await todoTable.DeleteAsync(item, ConflictsOnClient ? param : empty);
                items.Remove(item);
            }
            catch (MobileServiceInvalidOperationException e)
            {
                Messenger.Default.Send(new ShowDialogMessage("Error deleting item", exception.Message, new[] { new Button("OK", b => { }) }));
            }
            catch (MobileServiceConflictsResolvedException e)
            {
                exception = e;
            }

            if (exception != null)
            {
                await this.RefreshTodoItems();
            }
        }
    }
}
