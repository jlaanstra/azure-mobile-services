using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace Microsoft.Xaml.Interactivity
{
    public class Behavior<T> : DependencyObject, IBehavior where T : DependencyObject
    {
        public virtual void Attach(DependencyObject associatedObject)
        {
            AssociatedObject = associatedObject as T;
            OnAttached();
        }

        public virtual void Detach()
        {
            OnDetaching();
        }

        protected virtual void OnAttached()
        {
        }

        protected virtual void OnDetaching()
        {
        }

        public T AssociatedObject { get; private set; }

        DependencyObject IBehavior.AssociatedObject
        {
            get { return this.AssociatedObject; }
        }
    }
}
