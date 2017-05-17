using Everlook.Explorer;

namespace Everlook.Utility
{
	/// <summary>
	/// Some delegate signatures used internally for events.
	/// </summary>
	public static class CommunicationDelegates
	{
		/// <summary>
		/// A requested file action.
		/// </summary>
		/// <param name="page">The <see cref="GamePage"/> that the action originated from.</param>
		/// <param name="reference">The reference that the action is requested to be performed on.</param>
		public delegate void FileActionDelegate(GamePage page, FileReference reference);
	}
}