/*
* Copyright (c) 2024 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials
* File: MiddlewareController.cs 
*
* MiddlewareController.cs is part of VNLib.Plugins.Essentials which is part 
* of the larger VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Essentials is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System.Threading.Tasks;
using System.Collections.Generic;

namespace VNLib.Plugins.Essentials.Middleware
{
    internal sealed class MiddlewareController(EventProcessorConfig config)
    {
        private readonly IHttpMiddlewareChain _chain = config.MiddlewareChain;

        public async ValueTask<bool> ProcessAsync(HttpEntity entity)
        {
            /*
             * Loops through the current linkedlist of the current middleware chain. The 
             * chain should remain unmodified after GetCurrentHead() is called.
             * 
             * Middleware will return a Continue routine to move to the next middleware
             * node. All other routines mean the processor has responded to the client 
             * itself and must exit control and move to response.
             */

            LinkedListNode<IHttpMiddleware>? mwNode = _chain.GetCurrentHead();

            //Loop through nodes
            while (mwNode != null)
            {
                entity.EventArgs = await mwNode.ValueRef.ProcessAsync(entity);

                switch (entity.EventArgs.Routine)
                {
                    //move next if continue is returned
                    case FpRoutine.Continue:
                        break;

                    //Middleware completed the connection, time to exit the event processing
                    default:
                        return false;
                }

                mwNode = mwNode.Next;
            }

            return true;
        }

        public void PostProcess(HttpEntity entity)
        {
            /*
             * Middleware nodes may be allowed to inspect, or modify the return 
             * event arguments as the server may not have responded to the client
             * yet. 
             */

            LinkedListNode<IHttpMiddleware>? mwNode = _chain.GetCurrentHead();
           
            while (mwNode != null)
            {
                //Invoke mw handler on our event
                mwNode.ValueRef.VolatilePostProcess(entity, ref entity.EventArgs);

                mwNode = mwNode.Next;
            }
        }
    }
}
