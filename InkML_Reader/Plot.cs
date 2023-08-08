using System;

/**
 * @mainpage Plot
 * Read IoT Paper INK ML

MIT License

Copyright (c) 2023 Wacom co.,Ltd.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
 */

namespace InkML_Reader
{
	using int16_t = System.Int16;
	using uint16_t = System.UInt16;
	using int32_t = System.Int32;
	using uint32_t = System.UInt32;
	using uint8_t = System.Byte;

	public class Plot
	{

		private const uint CIRCLE6PIX = 6u;
		private const uint CIRCLE12PIX = 12u;
		private const uint CIRCLE16PIX = 16u;
		private const uint CIRCLE22PIX = 22u;

		private const uint STEP_CHECK_1 = CIRCLE6PIX;
		private const uint STEP_CHECK_2 = CIRCLE12PIX;		/* ~ 1step */
		private const uint STEP_CHECK_3 = CIRCLE16PIX;		/* ~ 4step */
		private const uint STEP_CHECK_4 = CIRCLE22PIX;      /* ~ 8step */

		private const byte STEP_NON = 0xff;

		private const byte STEP_VALUE_1 = 1;
		private const byte STEP_VALUE_2 = 2;
		private const byte STEP_VALUE_3 = 8;

		public struct plot_interpolation_str
		{
			public uint16_t x_point;
			public uint16_t y_point;
		};

		private const uint UL_CLR = 0;

		/* �⊮�o�b�t�@�T�C�Y */
		private const uint PLOT_INTERPOLATION_MAX = 512;

		public plot_interpolation_str[] plot_interpolation_buff;
		public uint32_t ful_plot_interpolation_cnt = 0;

		public Plot(double x1, double y1, double x2, double y2, InkML_brushRef mode, uint8_t bmp_pix)
        {
			/* ��ԃo�b�t�@ */
			plot_interpolation_buff = new plot_interpolation_str[PLOT_INTERPOLATION_MAX];

			plot_gr_line((uint16_t)x1, (uint16_t)y1, (uint16_t)x2, (uint16_t)y2, (InkML_brushRef)mode, (uint8_t) bmp_pix);
		}

		/******************************************************************************/
		/* Function Name	: prot_step_value										  */
		/* Arguments		: �~�T�C�Y�A�y��/�����S�����[�h								    */
		/* Return Value		: �X�e�b�v��												    */
		/* Description		: �~�T�C�Y���̃X�e�b�v��(�Ԉ�����)��ݒ�							 */
		/******************************************************************************/
		private uint8_t prot_step_value(uint8_t bmp_size, InkML_brushRef mode)
		{
			uint8_t uc_step;
			uc_step = STEP_NON;

			if (InkML_brushRef.ERASER == mode)
			{
				if (STEP_CHECK_4 <= bmp_size)
				{
					/* �Ԉ������Ȃ� */
				}
				else if (STEP_CHECK_3 <= bmp_size)
				{
					uc_step = STEP_VALUE_3;             /* STEP_VALUE_3�̐ݒ�l�����ŊԈ��� */
				}
				else if (STEP_CHECK_2 <= bmp_size)
				{
					uc_step = STEP_VALUE_2;             /* STEP_VALUE_2�̐ݒ�l�����ŊԈ��� */
				}
				else if (STEP_CHECK_1 <= bmp_size)
				{
					uc_step = STEP_VALUE_1;             /* STEP_VALUE_1�̐ݒ�l�����ŊԈ��� */
				}
				else
				{
				}
			}
			else
			{
			}

			return uc_step;
		}
		/******************************************************************************/
		/* Function Name	: plot_interpolation_resist								  */
		/* Arguments		: �⊮�o�b�t�@�֓o�^����X,Y���W								*/
		/* Return Value		: void													  */
		/* Description		: ��ԍ��W�̓o�^����										*/
		/******************************************************************************/
		private void plot_interpolation_resist(uint16_t x, uint16_t y)
		{
			if (PLOT_INTERPOLATION_MAX > ful_plot_interpolation_cnt)
			{
				plot_interpolation_buff[ful_plot_interpolation_cnt].x_point = x;
				plot_interpolation_buff[ful_plot_interpolation_cnt].y_point = y;
				ful_plot_interpolation_cnt++;
			}
			else
			{
			}
		}

		/******************************************************************************/
		/* Function Name	: plot_gr_line											  */
		/* Arguments		: ����X���W,����Y���W �O��X���W,�O��Y���W,				  */
		/*					  ���[�h(�y��/�����S��),�`��T�C�Y						     */
		/* Return Value		: void													  */
		/* Description		: �_�ԕ⊮����											  */
		/******************************************************************************/
		private void plot_gr_line(uint16_t x1, uint16_t y1, uint16_t x2, uint16_t y2, InkML_brushRef mode, uint8_t bmp_pix)
		{
			uint32_t ul_interpolation_step;
			uint32_t ul_interpolation_step_cnt;
			int32_t dx, dy, step, s;

			ful_plot_interpolation_cnt = UL_CLR;                            /* ��ԃo�b�t�@�̃J�E���^ */
			ul_interpolation_step_cnt = UL_CLR;                            /* �Ԉ����J�E���^ */
			ul_interpolation_step = prot_step_value(bmp_pix, mode);     /* �Ԉ����� */

			dx = Math.Abs(x2 - x1);                                                  //��Βl
			dy = Math.Abs(y2 - y1);                                                  //��Βl

			if (dx > dy)
			{                                                      //Y������X�����͂ǂ��炪�傫����
				//X�������傫���ꍇ
				if (x1 > x2)
				{                                                  //X1�_��,2�_�ڂ͂ǂ��炪�傫����
					//1�_�ڂ��傫��
					step = (y1 > y2) ? 1 : -1;                              //STEP�����͏㉺�ǂ��炩
					s = x1;
					x1 = x2;
					x2 = (uint16_t)s;
					y1 = y2;
				}
				else
				{
					//2�_�ڂ��傫��
					step = (y1 < y2) ? 1 : -1;                              //STEP�����͏㉺�ǂ��炩
				}
				s = (int32_t)dx >> 1;

				/* 1�_�� x,y */
				plot_interpolation_resist(x1, y1);

				while (++x1 <= x2)
				{
					if (0 > (s -= dy))
					{                                        //������X���K��+1 Y�͕K���ł͂Ȃ�
						s += dx;
						y1 = (uint16_t)(y1 + step);
					}

					/* �����Ŏc�蕪 x,y */
					ul_interpolation_step_cnt++;
					if (STEP_NON != ul_interpolation_step)
					{
						if (0u == (ul_interpolation_step_cnt % ul_interpolation_step))
						{
							plot_interpolation_resist(x1, y1);
						}
					}
					else
					{
						plot_interpolation_resist(x1, y1);
					}
				}
			}
			else
			{
				//Y�������傫���ꍇ
				if (y1 > y2)
				{                                                  //X1�_��,2�_�ڂ͂ǂ��炪�傫����
					//1�_�ڂ��傫��
					step = (x1 > x2) ? 1 : -1;                              //STEP�����͏㉺�ǂ��炩
					s = y1;
					y1 = y2;
					y2 = (uint16_t)s;
					x1 = x2;
				}
				else
				{
					//2�_�ڂ��傫��
					step = (x1 < x2) ? 1 : -1;                              //STEP�����͏㉺�ǂ��炩
				}
				s = (int32_t)dy >> 1;

				/* 1�_�� x,y */
				plot_interpolation_resist(x1, y1);

				while (++y1 <= y2)
				{
					if (0 > (s -= dx))
					{                                            //������X���K��+1 Y�͕K���ł͂Ȃ�
						s += dy;
						x1 = (uint16_t)(x1 + step);
					}

					/* �����Ŏc�蕪 x,y */
					ul_interpolation_step_cnt++;
					if (STEP_NON != ul_interpolation_step)
					{
						if (0u == (ul_interpolation_step_cnt % ul_interpolation_step))
						{
							plot_interpolation_resist(x1, y1);
						}
					}
					else
					{
						plot_interpolation_resist(x1, y1);
					}
				}

			}
		}
	}
}

