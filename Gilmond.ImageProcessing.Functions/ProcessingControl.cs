using System.Collections.Generic;

namespace Gilmond.ImageProcessing.Functions
{
    public class ProcessingControl
    {
        public string File { get; set; }
        public Queue<ProcessingInstruction> Instructions { get; set; }
    }
}
