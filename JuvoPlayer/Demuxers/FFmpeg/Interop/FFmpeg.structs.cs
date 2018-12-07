/*!
 * https://github.com/SamsungDForum/JuvoPlayer
 * Copyright 2018, Samsung Electronics Co., Ltd
 * Licensed under the MIT license
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
using System.Runtime.InteropServices;

namespace JuvoPlayer.Demuxers.FFmpeg.Interop
{
    internal unsafe struct _iobuf
    {
        public void* @_Placeholder;
    }

    internal unsafe struct AVRational
    {
        public int @num;
        public int @den;
    }

    internal unsafe struct AVClass
    {
        public byte* @class_name;
        public AVClass_item_name_func @item_name;
        public AVOption* @option;
        public int @version;
        public int @log_level_offset_offset;
        public int @parent_log_context_offset;
        public AVClass_child_next_func @child_next;
        public AVClass_child_class_next_func @child_class_next;
        public AVClassCategory @category;
        public AVClass_get_category_func @get_category;
        public AVClass_query_ranges_func @query_ranges;
    }

    internal unsafe struct AVOption
    {
        public byte* @name;
        public byte* @help;
        public int @offset;
        public AVOptionType @type;
        public AVOption_default_val @default_val;
        public double @min;
        public double @max;
        public int @flags;
        public byte* @unit;
    }

    internal unsafe struct AVOptionRanges
    {
        public AVOptionRange** @range;
        public int @nb_ranges;
        public int @nb_components;
    }

    internal unsafe struct AVFifoBuffer
    {
        public byte* @buffer;
        public byte* @rptr;
        public byte* @wptr;
        public byte* @end;
        public uint @rndx;
        public uint @wndx;
    }

    internal unsafe struct AVBufferRef
    {
        public AVBuffer* @buffer;
        public byte* @data;
        public int @size;
    }

    internal unsafe struct AVDictionaryEntry
    {
        public byte* @key;
        public byte* @value;
    }

    internal unsafe struct AVFrameSideData
    {
        public AVFrameSideDataType @type;
        public byte* @data;
        public int @size;
        public AVDictionary* @metadata;
        public AVBufferRef* @buf;
    }

    internal unsafe struct AVFrame
    {
        public byte_ptrArray8 @data;
        public int_array8 @linesize;
        public byte** @extended_data;
        public int @width;
        public int @height;
        public int @nb_samples;
        public int @format;
        public int @key_frame;
        public AVPictureType @pict_type;
        public AVRational @sample_aspect_ratio;
        public long @pts;
        public long @pkt_pts;
        public long @pkt_dts;
        public int @coded_picture_number;
        public int @display_picture_number;
        public int @quality;
        public void* @opaque;
        public ulong_array8 @error;
        public int @repeat_pict;
        public int @interlaced_frame;
        public int @top_field_first;
        public int @palette_has_changed;
        public long @reordered_opaque;
        public int @sample_rate;
        public ulong @channel_layout;
        public AVBufferRef_ptrArray8 @buf;
        public AVBufferRef** @extended_buf;
        public int @nb_extended_buf;
        public AVFrameSideData** @side_data;
        public int @nb_side_data;
        public int @flags;
        public AVColorRange @color_range;
        public AVColorPrimaries @color_primaries;
        public AVColorTransferCharacteristic @color_trc;
        public AVColorSpace @colorspace;
        public AVChromaLocation @chroma_location;
        public long @best_effort_timestamp;
        public long @pkt_pos;
        public long @pkt_duration;
        public AVDictionary* @metadata;
        public int @decode_error_flags;
        public int @channels;
        public int @pkt_size;
        public sbyte* @qscale_table;
        public int @qstride;
        public int @qscale_type;
        public AVBufferRef* @qp_table_buf;
        public AVBufferRef* @hw_frames_ctx;
        public AVBufferRef* @opaque_ref;
    }

    internal unsafe struct AVOption_default_val
    {
        public long @i64;
        public double @dbl;
        public byte* @str;
        public AVRational @q;
    }

    internal unsafe struct AVOptionRange
    {
        public byte* @str;
        public double @value_min;
        public double @value_max;
        public double @component_min;
        public double @component_max;
        public int @is_range;
    }

    internal unsafe struct AVComponentDescriptor
    {
        public int @plane;
        public int @step;
        public int @offset;
        public int @shift;
        public int @depth;
        public int @step_minus1;
        public int @depth_minus1;
        public int @offset_plus1;
    }

    internal unsafe struct AVPixFmtDescriptor
    {
        public byte* @name;
        public byte @nb_components;
        public byte @log2_chroma_w;
        public byte @log2_chroma_h;
        public ulong @flags;
        public AVComponentDescriptor_array4 @comp;
        public byte* @alias;
    }

    internal unsafe struct AVTimecode
    {
        public int @start;
        public uint @flags;
        public AVRational @rate;
        public uint @fps;
    }

    internal unsafe struct SwsVector
    {
        public double* @coeff;
        public int @length;
    }

    internal unsafe struct SwsFilter
    {
        public SwsVector* @lumH;
        public SwsVector* @lumV;
        public SwsVector* @chrH;
        public SwsVector* @chrV;
    }

    internal unsafe struct AVCodecDescriptor
    {
        public AVCodecID @id;
        public AVMediaType @type;
        public byte* @name;
        public byte* @long_name;
        public int @props;
        public byte** @mime_types;
        public AVProfile* @profiles;
    }

    internal unsafe struct AVProfile
    {
        public int @profile;
        public byte* @name;
    }

    internal unsafe struct RcOverride
    {
        public int @start_frame;
        public int @end_frame;
        public int @qscale;
        public float @quality_factor;
    }

    internal unsafe struct AVPanScan
    {
        public int @id;
        public int @width;
        public int @height;
        public short_arrayOfArray6 @position;
    }

    internal unsafe struct AVCPBProperties
    {
        public int @max_bitrate;
        public int @min_bitrate;
        public int @avg_bitrate;
        public int @buffer_size;
        public ulong @vbv_delay;
    }

    internal unsafe struct AVPacketSideData
    {
        public byte* @data;
        public int @size;
        public AVPacketSideDataType @type;
    }

    /*internal unsafe struct AVPacket
    {
        public AVBufferRef* @buf;
        public long @pts;
        public long @dts;
        public byte* @data;
        public int @size;
        public int @stream_index;
        public int @flags;
        public AVPacketSideData* @side_data;
        public int @side_data_elems;
        public long @duration;
        public long @pos;
        public long @convergence_duration;
    }*/

    internal unsafe struct AVPacket
    {
        public AVBufferRef* @buf;
        public System.Int64 @pts;
        public System.Int64 @dts;
        public byte* @data;
        public int @size;
        public int @stream_index;
        public int @flags;
        public AVPacketSideData* @side_data;
        public int @side_data_elems;
        public System.Int64 @duration;
        public System.Int64 @pos;
        public System.Int64 @convergence_duration;
    }

    internal unsafe struct AVEncBytes
    {
        public uint @bytes_of_clear_data;
        public uint @bytes_of_enc_data;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct AVEncInfo
    {
        public byte @iv_size;
        public byte_array16 @iv;
        public byte_array16 @kid;
        public byte @subsample_count;
        public AVEncBytes @subsamples;
    }

    internal unsafe struct AVCodecContext
    {
        public AVClass* @av_class;
        public int @log_level_offset;
        public AVMediaType @codec_type;
        public AVCodec* @codec;
        public byte_array32 @codec_name;
        public AVCodecID @codec_id;
        public uint @codec_tag;
        public uint @stream_codec_tag;
        public void* @priv_data;
        public AVCodecInternal* @internal;
        public void* @opaque;
        public long @bit_rate;
        public int @bit_rate_tolerance;
        public int @global_quality;
        public int @compression_level;
        public int @flags;
        public int @flags2;
        public byte* @extradata;
        public int @extradata_size;
        public AVRational @time_base;
        public int @ticks_per_frame;
        public int @delay;
        public int @width;
        public int @height;
        public int @coded_width;
        public int @coded_height;
        public int @gop_size;
        public AVPixelFormat @pix_fmt;
        public int @me_method;
        public AVCodecContext_draw_horiz_band_func @draw_horiz_band;
        public AVCodecContext_get_format_func @get_format;
        public int @max_b_frames;
        public float @b_quant_factor;
        public int @rc_strategy;
        public int @b_frame_strategy;
        public float @b_quant_offset;
        public int @has_b_frames;
        public int @mpeg_quant;
        public float @i_quant_factor;
        public float @i_quant_offset;
        public float @lumi_masking;
        public float @temporal_cplx_masking;
        public float @spatial_cplx_masking;
        public float @p_masking;
        public float @dark_masking;
        public int @slice_count;
        public int @prediction_method;
        public int* @slice_offset;
        public AVRational @sample_aspect_ratio;
        public int @me_cmp;
        public int @me_sub_cmp;
        public int @mb_cmp;
        public int @ildct_cmp;
        public int @dia_size;
        public int @last_predictor_count;
        public int @pre_me;
        public int @me_pre_cmp;
        public int @pre_dia_size;
        public int @me_subpel_quality;
        public int @dtg_active_format;
        public int @me_range;
        public int @intra_quant_bias;
        public int @inter_quant_bias;
        public int @slice_flags;
        public int @xvmc_acceleration;
        public int @mb_decision;
        public ushort* @intra_matrix;
        public ushort* @inter_matrix;
        public int @scenechange_threshold;
        public int @noise_reduction;
        public int @me_threshold;
        public int @mb_threshold;
        public int @intra_dc_precision;
        public int @skip_top;
        public int @skip_bottom;
        public float @border_masking;
        public int @mb_lmin;
        public int @mb_lmax;
        public int @me_penalty_compensation;
        public int @bidir_refine;
        public int @brd_scale;
        public int @keyint_min;
        public int @refs;
        public int @chromaoffset;
        public int @scenechange_factor;
        public int @mv0_threshold;
        public int @b_sensitivity;
        public AVColorPrimaries @color_primaries;
        public AVColorTransferCharacteristic @color_trc;
        public AVColorSpace @colorspace;
        public AVColorRange @color_range;
        public AVChromaLocation @chroma_sample_location;
        public int @slices;
        public AVFieldOrder @field_order;
        public int @sample_rate;
        public int @channels;
        public AVSampleFormat @sample_fmt;
        public int @frame_size;
        public int @frame_number;
        public int @block_align;
        public int @cutoff;
        public ulong @channel_layout;
        public ulong @request_channel_layout;
        public AVAudioServiceType @audio_service_type;
        public AVSampleFormat @request_sample_fmt;
        public AVCodecContext_get_buffer2_func @get_buffer2;
        public int @refcounted_frames;
        public float @qcompress;
        public float @qblur;
        public int @qmin;
        public int @qmax;
        public int @max_qdiff;
        public float @rc_qsquish;
        public float @rc_qmod_amp;
        public int @rc_qmod_freq;
        public int @rc_buffer_size;
        public int @rc_override_count;
        public RcOverride* @rc_override;
        public byte* @rc_eq;
        public long @rc_max_rate;
        public long @rc_min_rate;
        public float @rc_buffer_aggressivity;
        public float @rc_initial_cplx;
        public float @rc_max_available_vbv_use;
        public float @rc_min_vbv_overflow_use;
        public int @rc_initial_buffer_occupancy;
        public int @coder_type;
        public int @context_model;
        public int @lmin;
        public int @lmax;
        public int @frame_skip_threshold;
        public int @frame_skip_factor;
        public int @frame_skip_exp;
        public int @frame_skip_cmp;
        public int @trellis;
        public int @min_prediction_order;
        public int @max_prediction_order;
        public long @timecode_frame_start;
        public AVCodecContext_rtp_callback_func @rtp_callback;
        public int @rtp_payload_size;
        public int @mv_bits;
        public int @header_bits;
        public int @i_tex_bits;
        public int @p_tex_bits;
        public int @i_count;
        public int @p_count;
        public int @skip_count;
        public int @misc_bits;
        public int @frame_bits;
        public byte* @stats_out;
        public byte* @stats_in;
        public int @workaround_bugs;
        public int @strict_std_compliance;
        public int @error_concealment;
        public int @debug;
        public int @debug_mv;
        public int @err_recognition;
        public long @reordered_opaque;
        public AVHWAccel* @hwaccel;
        public void* @hwaccel_context;
        public ulong_array8 @error;
        public int @dct_algo;
        public int @idct_algo;
        public int @bits_per_coded_sample;
        public int @bits_per_raw_sample;
        public int @lowres;
        public AVFrame* @coded_frame;
        public int @thread_count;
        public int @thread_type;
        public int @active_thread_type;
        public int @thread_safe_callbacks;
        public AVCodecContext_execute_func @execute;
        public AVCodecContext_execute2_func @execute2;
        public int @nsse_weight;
        public int @profile;
        public int @level;
        public AVDiscard @skip_loop_filter;
        public AVDiscard @skip_idct;
        public AVDiscard @skip_frame;
        public byte* @subtitle_header;
        public int @subtitle_header_size;
        public int @error_rate;
        public ulong @vbv_delay;
        public int @side_data_only_packets;
        public int @initial_padding;
        public AVRational @framerate;
        public AVPixelFormat @sw_pix_fmt;
        public AVRational @pkt_timebase;
        public AVCodecDescriptor* @codec_descriptor;
        public long @pts_correction_num_faulty_pts;
        public long @pts_correction_num_faulty_dts;
        public long @pts_correction_last_pts;
        public long @pts_correction_last_dts;
        public byte* @sub_charenc;
        public int @sub_charenc_mode;
        public int @skip_alpha;
        public int @seek_preroll;
        public ushort* @chroma_intra_matrix;
        public byte* @dump_separator;
        public byte* @codec_whitelist;
        public uint @properties;
        public AVPacketSideData* @coded_side_data;
        public int @nb_coded_side_data;
        public AVBufferRef* @hw_frames_ctx;
        public int @sub_text_format;
        public int @trailing_padding;
        public long @max_pixels;
        public AVBufferRef* @hw_device_ctx;
        public int @hwaccel_flags;
    }

    internal unsafe struct AVCodec
    {
        public byte* @name;
        public byte* @long_name;
        public AVMediaType @type;
        public AVCodecID @id;
        public int @capabilities;
        public AVRational* @supported_framerates;
        public AVPixelFormat* @pix_fmts;
        public int* @supported_samplerates;
        public AVSampleFormat* @sample_fmts;
        public ulong* @channel_layouts;
        public byte @max_lowres;
        public AVClass* @priv_class;
        public AVProfile* @profiles;
        public int @priv_data_size;
        public AVCodec* @next;
        public AVCodec_init_thread_copy_func @init_thread_copy;
        public AVCodec_update_thread_context_func @update_thread_context;
        public AVCodecDefault* @defaults;
        public AVCodec_init_static_data_func @init_static_data;
        public AVCodec_init_func @init;
        public AVCodec_encode_sub_func @encode_sub;
        public AVCodec_encode2_func @encode2;
        public AVCodec_decode_func @decode;
        public AVCodec_close_func @close;
        public AVCodec_send_frame_func @send_frame;
        public AVCodec_send_packet_func @send_packet;
        public AVCodec_receive_frame_func @receive_frame;
        public AVCodec_receive_packet_func @receive_packet;
        public AVCodec_flush_func @flush;
        public int @caps_internal;
    }

    internal unsafe struct AVSubtitle
    {
        public ushort @format;
        public uint @start_display_time;
        public uint @end_display_time;
        public uint @num_rects;
        public AVSubtitleRect** @rects;
        public long @pts;
    }

    internal unsafe struct AVSubtitleRect
    {
        public int @x;
        public int @y;
        public int @w;
        public int @h;
        public int @nb_colors;
        public AVPicture @pict;
        public byte_ptrArray4 @data;
        public int_array4 @linesize;
        public AVSubtitleType @type;
        public byte* @text;
        public byte* @ass;
        public int @flags;
    }

    internal unsafe struct AVPicture
    {
        public byte_ptrArray8 @data;
        public int_array8 @linesize;
    }

    internal unsafe struct AVHWAccel
    {
        public byte* @name;
        public AVMediaType @type;
        public AVCodecID @id;
        public AVPixelFormat @pix_fmt;
        public int @capabilities;
        public AVHWAccel* @next;
        public AVHWAccel_alloc_frame_func @alloc_frame;
        public AVHWAccel_start_frame_func @start_frame;
        public AVHWAccel_decode_slice_func @decode_slice;
        public AVHWAccel_end_frame_func @end_frame;
        public int @frame_priv_data_size;
        public AVHWAccel_decode_mb_func @decode_mb;
        public AVHWAccel_init_func @init;
        public AVHWAccel_uninit_func @uninit;
        public int @priv_data_size;
        public int @caps_internal;
    }

    internal unsafe struct AVCodecParameters
    {
        public AVMediaType @codec_type;
        public AVCodecID @codec_id;
        public uint @codec_tag;
        public byte* @extradata;
        public int @extradata_size;
        public int @format;
        public long @bit_rate;
        public int @bits_per_coded_sample;
        public int @bits_per_raw_sample;
        public int @profile;
        public int @level;
        public int @width;
        public int @height;
        public AVRational @sample_aspect_ratio;
        public AVFieldOrder @field_order;
        public AVColorRange @color_range;
        public AVColorPrimaries @color_primaries;
        public AVColorTransferCharacteristic @color_trc;
        public AVColorSpace @color_space;
        public AVChromaLocation @chroma_location;
        public int @video_delay;
        public ulong @channel_layout;
        public int @channels;
        public int @sample_rate;
        public int @block_align;
        public int @frame_size;
        public int @initial_padding;
        public int @trailing_padding;
        public int @seek_preroll;
    }

    internal unsafe struct AVCodecParserContext
    {
        public void* @priv_data;
        public AVCodecParser* @parser;
        public long @frame_offset;
        public long @cur_offset;
        public long @next_frame_offset;
        public int @pict_type;
        public int @repeat_pict;
        public long @pts;
        public long @dts;
        public long @last_pts;
        public long @last_dts;
        public int @fetch_timestamp;
        public int @cur_frame_start_index;
        public long_array4 @cur_frame_offset;
        public long_array4 @cur_frame_pts;
        public long_array4 @cur_frame_dts;
        public int @flags;
        public long @offset;
        public long_array4 @cur_frame_end;
        public int @key_frame;
        public long @convergence_duration;
        public int @dts_sync_point;
        public int @dts_ref_dts_delta;
        public int @pts_dts_delta;
        public long_array4 @cur_frame_pos;
        public long @pos;
        public long @last_pos;
        public int @duration;
        public AVFieldOrder @field_order;
        public AVPictureStructure @picture_structure;
        public int @output_picture_number;
        public int @width;
        public int @height;
        public int @coded_width;
        public int @coded_height;
        public int @format;
    }

    internal unsafe struct AVCodecParser
    {
        public int_array5 @codec_ids;
        public int @priv_data_size;
        public AVCodecParser_parser_init_func @parser_init;
        public AVCodecParser_parser_parse_func @parser_parse;
        public AVCodecParser_parser_close_func @parser_close;
        public AVCodecParser_split_func @split;
        public AVCodecParser* @next;
    }

    internal unsafe struct AVBSFContext
    {
        public AVClass* @av_class;
        public AVBitStreamFilter* @filter;
        public AVBSFInternal* @internal;
        public void* @priv_data;
        public AVCodecParameters* @par_in;
        public AVCodecParameters* @par_out;
        public AVRational @time_base_in;
        public AVRational @time_base_out;
    }

    internal unsafe struct AVBitStreamFilter
    {
        public byte* @name;
        public AVCodecID* @codec_ids;
        public AVClass* @priv_class;
        public int @priv_data_size;
        public AVBitStreamFilter_init_func @init;
        public AVBitStreamFilter_filter_func @filter;
        public AVBitStreamFilter_close_func @close;
    }

    internal unsafe struct AVBitStreamFilterContext
    {
        public void* @priv_data;
        public AVBitStreamFilter* @filter;
        public AVCodecParserContext* @parser;
        public AVBitStreamFilterContext* @next;
        public byte* @args;
    }

    internal unsafe struct AVProbeData
    {
        public byte* @filename;
        public byte* @buf;
        public int @buf_size;
        public byte* @mime_type;
    }

    internal unsafe struct AVIndexEntry
    {
        public long @pos;
        public long @timestamp;
        public int @flags2_size30;
        public int @min_distance;
    }

    internal unsafe struct AVStream
    {
        public int @index;
        public int @id;
        public AVCodecContext* @codec;
        public void* @priv_data;
        public AVFrac @pts;
        public AVRational @time_base;
        public long @start_time;
        public long @duration;
        public long @nb_frames;
        public int @disposition;
        public AVDiscard @discard;
        public AVRational @sample_aspect_ratio;
        public AVDictionary* @metadata;
        public AVRational @avg_frame_rate;
        public AVPacket @attached_pic;
        public AVPacketSideData* @side_data;
        public int @nb_side_data;
        public int @event_flags;
        public AVStream_info* @info;
        public int @pts_wrap_bits;
        public long @first_dts;
        public long @cur_dts;
        public long @last_IP_pts;
        public int @last_IP_duration;
        public int @probe_packets;
        public int @codec_info_nb_frames;
        public AVStreamParseType @need_parsing;
        public AVCodecParserContext* @parser;
        public AVPacketList* @last_in_packet_buffer;
        public AVProbeData @probe_data;
        public long_array17 @pts_buffer;
        public AVIndexEntry* @index_entries;
        public int @nb_index_entries;
        public uint @index_entries_allocated_size;
        public AVRational @r_frame_rate;
        public int @stream_identifier;
        public long @interleaver_chunk_size;
        public long @interleaver_chunk_duration;
        public int @request_probe;
        public int @skip_to_keyframe;
        public int @skip_samples;
        public long @start_skip_samples;
        public long @first_discard_sample;
        public long @last_discard_sample;
        public int @nb_decoded_frames;
        public long @mux_ts_offset;
        public long @pts_wrap_reference;
        public int @pts_wrap_behavior;
        public int @update_initial_durations_done;
        public long_array17 @pts_reorder_error;
        public byte_array17 @pts_reorder_error_count;
        public long @last_dts_for_order_check;
        public byte @dts_ordered;
        public byte @dts_misordered;
        public int @inject_global_side_data;
        public byte* @recommended_encoder_configuration;
        public AVRational @display_aspect_ratio;
        public FFFrac* @priv_pts;
        public AVStreamInternal* @internal;
        public AVCodecParameters* @codecpar;
    }

    internal unsafe struct AVFrac
    {
        public long @val;
        public long @num;
        public long @den;
    }

    internal unsafe struct AVStream_info
    {
        public long @last_dts;
        public long @duration_gcd;
        public int @duration_count;
        public long @rfps_duration_sum;
        public double_arrayOfArray798* @duration_error;
        public long @codec_info_duration;
        public long @codec_info_duration_fields;
        public int @found_decoder;
        public long @last_duration;
        public long @fps_first_dts;
        public int @fps_first_dts_idx;
        public long @fps_last_dts;
        public int @fps_last_dts_idx;
    }

    internal unsafe struct AVPacketList
    {
        public AVPacket @pkt;
        public AVPacketList* @next;
    }

    internal unsafe struct AVProgram
    {
        public int @id;
        public int @flags;
        public AVDiscard @discard;
        public uint* @stream_index;
        public uint @nb_stream_indexes;
        public AVDictionary* @metadata;
        public int @program_num;
        public int @pmt_pid;
        public int @pcr_pid;
        public long @start_time;
        public long @end_time;
        public long @pts_wrap_reference;
        public int @pts_wrap_behavior;
    }

    internal unsafe struct AVChapter
    {
        public int @id;
        public AVRational @time_base;
        public long @start;
        public long @end;
        public AVDictionary* @metadata;
    }

    internal unsafe struct AVOutputFormat
    {
        public byte* @name;
        public byte* @long_name;
        public byte* @mime_type;
        public byte* @extensions;
        public AVCodecID @audio_codec;
        public AVCodecID @video_codec;
        public AVCodecID @subtitle_codec;
        public int @flags;
        public AVCodecTag** @codec_tag;
        public AVClass* @priv_class;
        public AVOutputFormat* @next;
        public int @priv_data_size;
        public AVOutputFormat_write_header_func @write_header;
        public AVOutputFormat_write_packet_func @write_packet;
        public AVOutputFormat_write_trailer_func @write_trailer;
        public AVOutputFormat_interleave_packet_func @interleave_packet;
        public AVOutputFormat_query_codec_func @query_codec;
        public AVOutputFormat_get_output_timestamp_func @get_output_timestamp;
        public AVOutputFormat_control_message_func @control_message;
        public AVOutputFormat_write_uncoded_frame_func @write_uncoded_frame;
        public AVOutputFormat_get_device_list_func @get_device_list;
        public AVOutputFormat_create_device_capabilities_func @create_device_capabilities;
        public AVOutputFormat_free_device_capabilities_func @free_device_capabilities;
        public AVCodecID @data_codec;
        public AVOutputFormat_init_func @init;
        public AVOutputFormat_deinit_func @deinit;
        public AVOutputFormat_check_bitstream_func @check_bitstream;
    }

    internal unsafe struct AVProtectionSystemSpecificData
    {
        public byte_array16 @system_id;
        public byte* @pssh_box;
        public uint @pssh_box_size;
    }

    internal unsafe struct AVFormatContext
    {
        public AVClass* @av_class;
        public AVInputFormat* @iformat;
        public AVOutputFormat* @oformat;
        public void* @priv_data;
        public AVIOContext* @pb;
        public int @ctx_flags;
        public uint @nb_streams;
        public AVStream** @streams;
        public byte_array1024 @filename;
        public long @start_time;
        public long @duration;
        public long @bit_rate;
        public uint @packet_size;
        public int @max_delay;
        public int @flags;
        public long @probesize;
        public long @max_analyze_duration;
        public byte* @key;
        public int @keylen;
        public uint @nb_programs;
        public AVProgram** @programs;
        public AVCodecID @video_codec_id;
        public AVCodecID @audio_codec_id;
        public AVCodecID @subtitle_codec_id;
        public uint @max_index_size;
        public uint @max_picture_buffer;
        public uint @nb_chapters;
        public AVChapter** @chapters;
        public AVDictionary* @metadata;
        public long @start_time_realtime;
        public int @fps_probe_size;
        public int @error_recognition;
        public AVIOInterruptCB @interrupt_callback;
        public int @debug;
        public long @max_interleave_delta;
        public int @strict_std_compliance;
        public int @event_flags;
        public int @max_ts_probe;
        public int @avoid_negative_ts;
        public int @ts_id;
        public int @audio_preload;
        public int @max_chunk_duration;
        public int @max_chunk_size;
        public int @use_wallclock_as_timestamps;
        public int @avio_flags;
        public AVDurationEstimationMethod @duration_estimation_method;
        public long @skip_initial_bytes;
        public uint @correct_ts_overflow;
        public int @seek2any;
        public int @flush_packets;
        public int @probe_score;
        public int @format_probesize;
        public byte* @codec_whitelist;
        public byte* @format_whitelist;
        public AVFormatInternal* @internal;
        public int @io_repositioned;
        public AVCodec* @video_codec;
        public AVCodec* @audio_codec;
        public AVCodec* @subtitle_codec;
        public AVCodec* @data_codec;
        public int @metadata_header_padding;
        public void* @opaque;
        public AVFormatContext_control_message_cb_func @control_message_cb;
        public long @output_ts_offset;
        public byte* @dump_separator;
        public AVCodecID @data_codec_id;
        public AVFormatContext_open_cb_func @open_cb;
        public byte* @protocol_whitelist;
        public AVFormatContext_io_open_func @io_open;
        public AVFormatContext_io_close_func @io_close;
        public byte* @protocol_blacklist;
        public int @max_streams;
        public AVProtectionSystemSpecificData* @protection_system_data;
        public uint @protection_system_data_count;
    }

    internal unsafe struct AVInputFormat
    {
        public byte* @name;
        public byte* @long_name;
        public int @flags;
        public byte* @extensions;
        public AVCodecTag** @codec_tag;
        public AVClass* @priv_class;
        public byte* @mime_type;
        public AVInputFormat* @next;
        public int @raw_codec_id;
        public int @priv_data_size;
        public AVInputFormat_read_probe_func @read_probe;
        public AVInputFormat_read_header_func @read_header;
        public AVInputFormat_read_packet_func @read_packet;
        public AVInputFormat_read_close_func @read_close;
        public AVInputFormat_read_seek_func @read_seek;
        public AVInputFormat_read_timestamp_func @read_timestamp;
        public AVInputFormat_read_play_func @read_play;
        public AVInputFormat_read_pause_func @read_pause;
        public AVInputFormat_read_seek2_func @read_seek2;
        public AVInputFormat_get_device_list_func @get_device_list;
        public AVInputFormat_create_device_capabilities_func @create_device_capabilities;
        public AVInputFormat_free_device_capabilities_func @free_device_capabilities;
    }

    internal unsafe struct AVDeviceInfoList
    {
        public AVDeviceInfo** @devices;
        public int @nb_devices;
        public int @default_device;
    }

    internal unsafe struct AVDeviceCapabilitiesQuery
    {
        public AVClass* @av_class;
        public AVFormatContext* @device_context;
        public AVCodecID @codec;
        public AVSampleFormat @sample_format;
        public AVPixelFormat @pixel_format;
        public int @sample_rate;
        public int @channels;
        public long @channel_layout;
        public int @window_width;
        public int @window_height;
        public int @frame_width;
        public int @frame_height;
        public AVRational @fps;
    }

    internal unsafe struct AVIOContext
    {
        public AVClass* @av_class;
        public byte* @buffer;
        public int @buffer_size;
        public byte* @buf_ptr;
        public byte* @buf_end;
        public void* @opaque;
        public AVIOContext_read_packet_func @read_packet;
        public AVIOContext_write_packet_func @write_packet;
        public AVIOContext_seek_func @seek;
        public long @pos;
        public int @must_flush;
        public int @eof_reached;
        public int @write_flag;
        public int @max_packet_size;
        public ulong @checksum;
        public byte* @checksum_ptr;
        public AVIOContext_update_checksum_func @update_checksum;
        public int @error;
        public AVIOContext_read_pause_func @read_pause;
        public AVIOContext_read_seek_func @read_seek;
        public int @seekable;
        public long @maxsize;
        public int @direct;
        public long @bytes_read;
        public int @seek_count;
        public int @writeout_count;
        public int @orig_buffer_size;
        public int @short_seek_threshold;
        public byte* @protocol_whitelist;
        public byte* @protocol_blacklist;
        public AVIOContext_write_data_type_func @write_data_type;
        public int @ignore_boundary_point;
        public AVIODataMarkerType @current_type;
        public long @last_time;
        public AVIOContext_short_seek_get_func @short_seek_get;
    }

    internal unsafe struct AVIOInterruptCB
    {
        public AVIOInterruptCB_callback_func @callback;
        public void* @opaque;
    }

    internal unsafe struct AVIODirEntry
    {
        public byte* @name;
        public int @type;
        public int @utf8;
        public long @size;
        public long @modification_timestamp;
        public long @access_timestamp;
        public long @status_change_timestamp;
        public long @user_id;
        public long @group_id;
        public long @filemode;
    }

    internal unsafe struct AVIODirContext
    {
        public URLContext* @url_context;
    }

    internal unsafe struct AVFilterContext
    {
        public AVClass* @av_class;
        public AVFilter* @filter;
        public byte* @name;
        public AVFilterPad* @input_pads;
        public AVFilterLink** @inputs;
        public uint @nb_inputs;
        public AVFilterPad* @output_pads;
        public AVFilterLink** @outputs;
        public uint @nb_outputs;
        public void* @priv;
        public AVFilterGraph* @graph;
        public int @thread_type;
        public AVFilterInternal* @internal;
        public AVFilterCommand* @command_queue;
        public byte* @enable_str;
        public void* @enable;
        public double* @var_values;
        public int @is_disabled;
        public AVBufferRef* @hw_device_ctx;
        public int @nb_threads;
        public uint @ready;
    }

    internal unsafe struct AVFilter
    {
        public byte* @name;
        public byte* @description;
        public AVFilterPad* @inputs;
        public AVFilterPad* @outputs;
        public AVClass* @priv_class;
        public int @flags;
        public AVFilter_init_func @init;
        public AVFilter_init_dict_func @init_dict;
        public AVFilter_uninit_func @uninit;
        public AVFilter_query_formats_func @query_formats;
        public int @priv_size;
        public int @flags_internal;
        public AVFilter* @next;
        public AVFilter_process_command_func @process_command;
        public AVFilter_init_opaque_func @init_opaque;
        public AVFilter_activate_func @activate;
    }

    internal unsafe struct AVFilterLink
    {
        public AVFilterContext* @src;
        public AVFilterPad* @srcpad;
        public AVFilterContext* @dst;
        public AVFilterPad* @dstpad;
        public AVMediaType @type;
        public int @w;
        public int @h;
        public AVRational @sample_aspect_ratio;
        public ulong @channel_layout;
        public int @sample_rate;
        public int @format;
        public AVRational @time_base;
        public AVFilterFormats* @in_formats;
        public AVFilterFormats* @out_formats;
        public AVFilterFormats* @in_samplerates;
        public AVFilterFormats* @out_samplerates;
        public AVFilterChannelLayouts* @in_channel_layouts;
        public AVFilterChannelLayouts* @out_channel_layouts;
        public int @request_samples;
        public AVFilterLink_init_state @init_state;
        public AVFilterGraph* @graph;
        public long @current_pts;
        public long @current_pts_us;
        public int @age_index;
        public AVRational @frame_rate;
        public AVFrame* @partial_buf;
        public int @partial_buf_size;
        public int @min_samples;
        public int @max_samples;
        public int @channels;
        public uint @flags;
        public long @frame_count_in;
        public long @frame_count_out;
        public void* @frame_pool;
        public int @frame_wanted_out;
        public AVBufferRef* @hw_frames_ctx;
        public byte_array61440 @reserved;
    }

    internal unsafe struct AVFilterGraph
    {
        public AVClass* @av_class;
        public AVFilterContext** @filters;
        public uint @nb_filters;
        public byte* @scale_sws_opts;
        public byte* @resample_lavr_opts;
        public int @thread_type;
        public int @nb_threads;
        public AVFilterGraphInternal* @internal;
        public void* @opaque;
        public AVFilterGraph_execute_func @execute;
        public byte* @aresample_swr_opts;
        public AVFilterLink** @sink_links;
        public int @sink_links_count;
        public uint @disable_auto_convert;
    }

    internal unsafe struct AVFilterInOut
    {
        public byte* @name;
        public AVFilterContext* @filter_ctx;
        public int @pad_idx;
        public AVFilterInOut* @next;
    }

    internal unsafe struct AVBufferSrcParameters
    {
        public int @format;
        public AVRational @time_base;
        public int @width;
        public int @height;
        public AVRational @sample_aspect_ratio;
        public AVRational @frame_rate;
        public AVBufferRef* @hw_frames_ctx;
        public int @sample_rate;
        public ulong @channel_layout;
    }

    internal unsafe struct AVBufferSinkParams
    {
        public AVPixelFormat* @pixel_fmts;
    }

    internal unsafe struct AVABufferSinkParams
    {
        public AVSampleFormat* @sample_fmts;
        public long* @channel_layouts;
        public int* @channel_counts;
        public int @all_channel_counts;
        public int* @sample_rates;
    }

    internal unsafe struct AVDeviceInfo
    {
        public byte* @device_name;
        public byte* @device_description;
    }

    internal unsafe struct AVDeviceRect
    {
        public int @x;
        public int @y;
        public int @width;
        public int @height;
    }

}
