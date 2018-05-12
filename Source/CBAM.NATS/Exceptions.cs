using System;
using System.Collections.Generic;
using System.Text;

namespace CBAM.NATS
{
   public sealed class NATSException : Exception
   {
      public NATSException( String msg, Exception inner = null )
          : base( msg, inner )
      {
      }
   }
}
