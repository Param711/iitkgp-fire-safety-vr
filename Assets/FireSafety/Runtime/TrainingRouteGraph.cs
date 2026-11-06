using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireSafety
{
    [Serializable] public class RouteNode
    {
        public string id;
        public Vector3 position;
        public RouteKind kind;
        public bool secondaryExit;
        public List<int> neighbors=new List<int>();
    }
    public class TrainingRouteGraph : MonoBehaviour
    {
        public List<RouteNode> nodes=new List<RouteNode>();
        public int Add(string id, Vector3 p, RouteKind kind=RouteKind.Corridor)
        { nodes.Add(new RouteNode {id=id,position=p,kind=kind}); return nodes.Count-1; }
        public void Link(int a,int b) { if(!nodes[a].neighbors.Contains(b))nodes[a].neighbors.Add(b); if(!nodes[b].neighbors.Contains(a))nodes[b].neighbors.Add(a); }
        public int Nearest(Vector3 position, bool primaryBlocked=false, bool secondaryBlocked=false)
        {
            int best=-1; float d=float.MaxValue;
            for(int i=0;i<nodes.Count;i++)
            {
                if(!AssessmentRules.RouteAvailable(nodes[i].kind,primaryBlocked,secondaryBlocked))continue;
                float n=(nodes[i].position-position).sqrMagnitude;
                if(n<d){d=n;best=i;}
            }
            return best;
        }
        public List<int> FindPath(int start, Predicate<RouteNode> destination, bool primaryBlocked, bool secondaryBlocked,bool exitBlocked=false,Func<RouteNode,RouteNode,bool> edgeSafe=null,bool secondaryExitBlocked=false)
        { return RoutePlanner.FindPath(nodes,start,destination,primaryBlocked,secondaryBlocked,exitBlocked,edgeSafe,secondaryExitBlocked); }
    }
    public static class RoutePlanner
    {
        public static List<int> FindPath(List<RouteNode> nodes,int start,Predicate<RouteNode> destination,bool primaryBlocked,bool secondaryBlocked,bool exitBlocked=false,Func<RouteNode,RouteNode,bool> edgeSafe=null,bool secondaryExitBlocked=false)
        {
            var result=new List<int>(); if(start<0 || start>=nodes.Count)return result;
            var cost=new float[nodes.Count]; var prev=new int[nodes.Count]; var visited=new bool[nodes.Count];
            for(int i=0;i<nodes.Count;i++){cost[i]=float.PositiveInfinity;prev[i]=-1;} cost[start]=0;
            int end=-1;
            for(int step=0;step<nodes.Count;step++)
            {
                int u=-1; float best=float.PositiveInfinity;
                for(int i=0;i<nodes.Count;i++)if(!visited[i] && cost[i]<best){u=i;best=cost[i];}
                if(u<0)break; visited[u]=true;
                if(destination(nodes[u])){end=u;break;}
                foreach(int v in nodes[u].neighbors)
                {
                    if(v<0 || v>=nodes.Count || !AssessmentRules.RouteAvailable(nodes[v].kind,primaryBlocked,secondaryBlocked) || (nodes[v].kind==RouteKind.Outside&&(nodes[v].secondaryExit?secondaryExitBlocked:exitBlocked)))continue;
                    if(edgeSafe!=null&&!edgeSafe(nodes[u],nodes[v]))continue;
                    float candidate=cost[u]+Vector3.Distance(nodes[u].position,nodes[v].position);
                    if(candidate<cost[v]){cost[v]=candidate;prev[v]=u;}
                }
            }
            if(end<0)return result;
            for(int at=end;at>=0;at=prev[at])result.Add(at);
            result.Reverse(); return result;
        }
    }
}
