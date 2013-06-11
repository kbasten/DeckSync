using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ApiDeckMessage
{
    public String msg;
    public ApiDeckData data;
}
public class ApiDeckData
{
    public String name;
    public DeckScroll[] scrolls;

}
public class DeckScroll
{
    public int id;
    public int c;
}