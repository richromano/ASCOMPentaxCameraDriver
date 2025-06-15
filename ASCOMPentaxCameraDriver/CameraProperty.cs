using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ASCOM.PentaxKP
{
    public class CameraProperty : PentaxKPCommon
    {
        private PropertyDescriptor m_descriptor;
        private PropertyValueOption[] m_options;

        public CameraProperty(PropertyDescriptor descriptor, PropertyValueOption[] options)
        {
            m_descriptor = descriptor;
            m_options = options;
        }

        public PropertyValue CurrentValue(Ricoh.CameraController.CameraDevice camera)
        {
            PropertyValue v = new PropertyValue();

            //camera.GetCameraDeviceSettings({ PROPERTY_ISO});// GetSinglePropertyValue(hCamera, m_descriptor.Id, ref v);

            return v;
        }

        public PropertyDescriptor Descriptor
        {
            get
            {
                return m_descriptor;
            }
        }

        public PropertyValueOption[] Options
        {
            get
            {
                return m_options;
            }
        }
    }
}
