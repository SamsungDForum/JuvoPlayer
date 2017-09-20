

/**
 * @author p.galiszewsk
 * @version 1.0
 * @created 20-wrz-2017 14:10:13
 */
public class UI {

	/**
	 * @author p.galiszewsk
	 * @version 1.0
	 * @created 20-wrz-2017 14:10:13
	 */
	public interface IUIController {

		/**
		 * 
		 * @param representation
		 */
		public void ChangeRepresentation(int representation);

		public void OnBufferingCompleted();

		/**
		 * 
		 * @param subtitle
		 */
		public void OnRenderSubtitle(Subtitle subtitle);

		/**
		 * 
		 * @param clips
		 */
		public void OnSetClips(List<ClipDefinition> clips);

		/**
		 * 
		 * @param time
		 */
		public void OnTimeUpdated(double time);

		public void Play();

		/**
		 * 
		 * @param file
		 */
		public void SetExternalSubtitle(string file);

		/**
		 * 
		 * @param clip
		 */
		public void ShowClip(ClipDefinition clip);

		public void Stop();

		/**
		 * 
		 * @param position
		 */
		public void TimeUpdated(double position);

	}

	public UI(){

	}

	public void finalize() throws Throwable {

	}

}