using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GIMcompress;
using GIMtransfer.write;
using GimUtilities;
using GIMocctCLI;
using static System.Net.Mime.MediaTypeNames;
using System.IO;

namespace GIMtransfer.read.IFCReader
{
    public class GimBooleanConvertion
    {
        public static void TransformBooleanToElement(GimData gimData)
        {
            for(int i = 0; i < gimData.Elements.Count; i++) 
            { 
                GimElement currentElement = gimData.Elements[i];
                if(currentElement.elementType == GimElementType.Gim_Boolean)
                {
                    int firstOperandID = (int)currentElement.Elements[0];
                    if (firstOperandID == 38401 || firstOperandID == 38402)
                    {
                        //continue;
                    }

                    GimElement firstOperandElement = gimData.Elements[firstOperandID];
                    //if (firstOperandElement.elementType == GimElementType.Gim_Boolean)
                    //  TreatBooleanElements(gimData, firstOperandElement);
                    //else
                        TreatBooleanElements(gimData, currentElement);
                    //int partID = GetParentID(gimData, currentElement);
                    i--;
                }
            }
        }




        public static void TreatChildBooleanElements(GimData gimData, GimElement gimElement, int parentID)
        {
            
            int firstOperandID = (int)gimElement.Elements[0];
            GimElement firstOperandElement = gimData.Elements[firstOperandID];
            if (firstOperandElement.elementType == GimElementType.Gim_Boolean)
            {
                TreatChildBooleanElements(gimData, firstOperandElement, firstOperandID);
                firstOperandID = (int)gimElement.Elements[0];
            }

            var tempGIM1 = gimData.GetGimElementAsGimData(gimData, (uint)firstOperandID);
            tempGIM1.CreateRootIfNotExists();
            int secondOperandID = (int)gimElement.Elements[1];
            var tempGIM2 = gimData.GetGimElementAsGimData(gimData, (uint)secondOperandID);
            tempGIM2.CreateRootIfNotExists();
            GimProgressStatus gimProgressStatus = new GimProgressStatus();
            GimProgressStatus gimProgressStatus2 = new GimProgressStatus();
            GimProgressStatus gimProgressStatus3 = new GimProgressStatus();
            GimProgressStatus gimProgressStatus4 = new GimProgressStatus();
            GimProgressStatus gimProgressStatus5 = new GimProgressStatus();
            GimProgressStatus gimProgressStatus6 = new GimProgressStatus();
            GimProgressStatus gimProgressStatus7 = new GimProgressStatus();
            GIMtransfer.GIMocctWriteElements occt = new(gimProgressStatus);
            GIMtransfer.GIMocctWriteElements occt2 = new(gimProgressStatus2);
            GIMtransfer.GIMocctReadElements occt3 = new(gimProgressStatus3);
            GIMtransfer.GIMocctReadElements occt4 = new(gimProgressStatus6);
            GIMtransfer.GIMocctReadElements occt5 = new(gimProgressStatus7);
            //GIMtransfer.GIMocctReadElements occt6 = new(gimProgressStatus3);
            string gimTempPath = FileAccess.GetTempFolder();
            string booleanOpPath = gimTempPath + "operations";
            Directory.CreateDirectory(booleanOpPath);
            string filePath1 = booleanOpPath + "\\input1";
            string filePath2 = booleanOpPath + "\\input2";
            string filePath3 = booleanOpPath + "\\finalOUTPUT.step";
            occt.WriteGimDataToOcct(filePath1, "step", tempGIM1);
            occt2.WriteGimDataToOcct(filePath2, "step", tempGIM2);
            TestBoolWrapper testBool = new(filePath1 + ".step", filePath2 + ".step");
            testBool.PerformCut(filePath3);
            GimData finalGIM = new GimData();
            try
            {
                testBool.PerformCut(filePath3);
                finalGIM = occt4.ReadToGimData(filePath3, "step");
            }
            catch
            {
                finalGIM = tempGIM1;
            }
            
            int partID = GetParentID(gimData, gimElement);
            GimElement parentElement = gimData.elements[parentID];
            int parentPartID = (int)parentElement.Locals[0].Parent;

            List<uint> partToRemove = new();
            partToRemove.Add((uint)parentID);
            gimData.RemoveElementsAndTheirChildren(partToRemove);
            gimData.AddSecondaryGimData(finalGIM, 1, (uint)parentPartID, 0);
            GimElement parentPart = gimData.elements[parentPartID];
            var temp = parentPart.elements[0];
            parentPart.elements[0] = parentPart.elements[1];
            parentPart.elements[1] = temp;
            var a = 0;


        }


       




        public static void TreatBooleanElements(GimData gimData, GimElement gimElement)
        {
            int firstOperandID = (int)gimElement.Elements[0];
            GimElement firstOperandElement = gimData.Elements[firstOperandID];
            if (firstOperandElement.elementType == GimElementType.Gim_Boolean)
            {
                TreatChildBooleanElements(gimData, firstOperandElement, firstOperandID);
                firstOperandID = (int)gimElement.Elements[0];
            }
            
            var tempGIM1 = gimData.GetGimElementAsGimData(gimData, (uint)firstOperandID);
            tempGIM1.CreateRootIfNotExists();
            int secondOperandID = (int)gimElement.Elements[1];
            var tempGIM2 = gimData.GetGimElementAsGimData(gimData, (uint)secondOperandID);
            tempGIM2.CreateRootIfNotExists(); 
            GimProgressStatus gimProgressStatus = new GimProgressStatus();
            GimProgressStatus gimProgressStatus2 = new GimProgressStatus();
            GimProgressStatus gimProgressStatus3 = new GimProgressStatus();
            GimProgressStatus gimProgressStatus4 = new GimProgressStatus();
            GimProgressStatus gimProgressStatus5 = new GimProgressStatus();
            GimProgressStatus gimProgressStatus6 = new GimProgressStatus();
            GimProgressStatus gimProgressStatus7 = new GimProgressStatus();
            GIMtransfer.GIMocctWriteElements occt = new(gimProgressStatus);            
            GIMtransfer.GIMocctWriteElements occt2 = new(gimProgressStatus2);
            GIMtransfer.GIMocctReadElements occt3 = new(gimProgressStatus3);
            GIMtransfer.GIMocctReadElements occt4 = new(gimProgressStatus6);
            GIMtransfer.GIMocctReadElements occt5 = new(gimProgressStatus7);
            //GIMtransfer.GIMocctReadElements occt6 = new(gimProgressStatus3);

            string gimTempPath = FileAccess.GetTempFolder();
            string booleanOpPath = gimTempPath + "operations";
            Directory.CreateDirectory(booleanOpPath);
            string filePath1 = booleanOpPath + "\\input1";
            string filePath2 = booleanOpPath + "\\input2";
            string filePath3 = booleanOpPath + "\\finalOUTPUT.step";
            occt.WriteGimDataToOcct(filePath1, "step", tempGIM1);
            occt2.WriteGimDataToOcct(filePath2, "step", tempGIM2);
            TestBoolWrapper testBool = new(filePath1 +".step", filePath2 +".step");
            GimData finalGIM = new GimData();
            try
            {
                testBool.PerformCut(filePath3);
               finalGIM = occt4.ReadToGimData(filePath3, "step");
            }
            catch
            {
                 finalGIM = tempGIM1;
            }
            
            int partID = GetParentID(gimData, gimElement);
            GimElement partElement = gimData.elements[partID];
            int parentPartID = (int)partElement.Locals[0].Parent;

            List<uint> partToRemove = new();
            partToRemove.Add((uint)partID);
            gimData.RemoveElementsAndTheirChildren(partToRemove);
            gimData.AddSecondaryGimData(finalGIM, 1, (uint)parentPartID, 0);

            

            


            BOPAlgo_BOPWrapper bopCutter = new();
            
            //var resultShape = bopCutter.PrepareCut(tempOCCT1, tempOCCT2);
            ////BRepAlgoCutWrapper bopCutter = new(tempOCCT1, tempOCCT2);
            ////bopCutter.Build();
            ////var resultShape = bopCutter.Shape();
            ////foreach (string warning in bopCutter.GetWarnings())
            ////{
            ////    Console.WriteLine("Warning: " + warning);
            ////}
            ////foreach (string error in bopCutter.GetErrors())
            ////{
            ////    Console.WriteLine("Warning: " + error);
            ////}

            
            
            //var resultingGimData = occt3.ReadToGimDataFromIFC(resultShape);
            
            //AssimpWriteElements ack2 = new(gimProgressStatus5);
            //GIMtransfer.GIMocctWriteElements ack = new(gimProgressStatus4);
            //string filePath = "replace";
            //ack2.WriteGimDataToAssimp(filePath, "glb", resultingGimData);
            //ack.WriteGimDataToOcct(filePath, "step", resultingGimData);
            
            //gimProgressStatus = new GimProgressStatus();
                    var a = 0;

            
        }

        public static int GetParentID(GimData gimData, GimElement gimElement) 
        {
            
            int parentID = (int)gimElement.Locals[0].Parent;
            GimElement parentElement = gimData.Elements[parentID];
            if(parentElement.ElementType != GimElementType.D_part)
               return GetParentID(gimData, parentElement);
            return parentID;
        }

    }
}
