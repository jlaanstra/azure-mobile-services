﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.WindowsAzure.MobileServices;

namespace Todo.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IMobileServiceClient client;
        private readonly NetworkInformationDelegate networkDelegate;

        private readonly IMobileServiceTable<TodoItem2> todoTable;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public MainViewModel(IMobileServiceClient client, NetworkInformationDelegate networkDelegate)
        {
            this.client = client;
            this.networkDelegate = networkDelegate;

            this.todoTable = client.GetTable<TodoItem2>();

            this.RefreshCommand = new RelayCommand(this.RefreshTodoItems);
            this.SaveCommand = new RelayCommand(this.Save);
            this.CompletedCommand = new RelayCommand<TodoItem2>(x => this.Complete(x));
            this.RemoveCommand = new RelayCommand<TodoItem2>(x => this.Remove(x));

            this.Items = new ObservableCollection<TodoItem2>();

            //this.RefreshTodoItems();
        }

        public RelayCommand RefreshCommand { get; set; }
        public RelayCommand<TodoItem2> RemoveCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }
        public RelayCommand<TodoItem2> CompletedCommand { get; set; }

        private ObservableCollection<TodoItem2> items;
        public ObservableCollection<TodoItem2> Items
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

        private async Task InsertTodoItem(TodoItem2 todoItem)
        {
            // This code inserts a new TodoItem into the database. When the operation completes
            // and Mobile Services has assigned an Id, the item is added to the CollectionView
            await todoTable.InsertAsync(todoItem);
            Items.Add(todoItem);
        }

        private async void RefreshTodoItems()
        {
            try
            {
                // This code refreshes the entries in the list view be querying the TodoItems table.
                // The query excludes completed TodoItems
                Items = await todoTable
                    .Where(todoItem => todoItem.Complete == false)
                    .IncludeTotalCount()
                    .ToCollectionAsync();
            }
            catch
            {
                Items = null;
            }
        }

        private async void Save()
        {
            var todoItem = new TodoItem2 { Text = this.Text, Id = Guid.NewGuid() };
            try
            {
                await InsertTodoItem(todoItem);
            }
            catch (Exception e)
            {

            }
        }

        private async void Complete(TodoItem2 item)
        {
            try
            {
                // This code takes a freshly completed TodoItem and updates the database. When the MobileService 
                // responds, the item is removed from the list 
                item.Complete = true;
                await todoTable.UpdateAsync(item);
                items.Remove(item);
            }
            catch
            {

            }
        }

        private async void Remove(TodoItem2 item)
        {
            try
            {
                // This code takes a freshly completed TodoItem and updates the database. When the MobileService 
                // responds, the item is removed from the list 
                await todoTable.DeleteAsync(item);
                items.Remove(item);
            }
            catch
            {

            }
        }
    }
}
