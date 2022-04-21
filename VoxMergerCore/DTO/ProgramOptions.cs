namespace VoxMerger.DTO
{
	public class ProgramOptions
	{
		public string InputFolder { get; set; }
		public string OutputFile { get; set; }
		public bool ShopHelp { get; set; }
		public bool EnableDebug { get; set; }
		public bool EnableLogs { get; set; }
		public bool Union { get; set; }

		public string GetSageOutputFile()
		{
			return OutputFile.Contains(".vox") ? OutputFile : OutputFile + ".vox";

		}
	}
}
